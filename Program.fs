module WinTail.Program

open System
open System.Threading.Tasks
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Akka.Actor
open Akkling


open WinTail.Actors


type AkkaHostedService() =
    let winTailConfig = Configuration.defaultConfig()
    let winTailSystem = System.create "WinTailSystem" winTailConfig

    let consoleWriterRef = 
        Actors.consoleWriter
        |> actorOf2 
        |> props 
        |> spawn winTailSystem "ConsoleWriter"

    let tailCoordinatorRef = 
        let actor = actorOf2 Actors.tailCoordinator
        let super = 
            Strategy.OneForOne(
                fun ex ->
                    match ex with
                    | :? NotSupportedException -> Directive.Stop
                    | _ -> Directive.Resume
                , 3
                , TimeSpan.FromSeconds(30.0)
            )
        let props = { props actor with SupervisionStrategy = Some super }
        spawn winTailSystem "TailCoordinator" props

    let inputValidatorRef =
        Actors.inputValidator consoleWriterRef
        |> actorOf2
        |> props
        |> spawn winTailSystem "InputValidator"    

    let consoleReaderRef = 
        Actors.consoleReader
        |> actorOf2 
        |> props 
        |> spawn winTailSystem "ConsoleReader"

    interface IHostedService with
        member this.StartAsync(cancellation) =
            Console.WriteLine "Enter the path to the file you want to tail:"
            
            consoleReaderRef <! Messages.ReadInput

            Task.CompletedTask

        member this.StopAsync(cancellation) =
            winTailSystem.Terminate()

    interface IDisposable with
        member this.Dispose() =
            winTailSystem.Dispose()


[<EntryPoint>]
let main args =
    HostBuilder()
        .ConfigureServices(fun collection ->
            collection
                .Configure<ConsoleLifetimeOptions>(fun options ->
                    options.SuppressStatusMessages <- true
                )
                .AddHostedService<AkkaHostedService>()
                |> ignore
        )
        .UseConsoleLifetime()
        .Build()
        .Run()

    0 // return an integer exit code
