module Program

open System
open Akkling
open Akka.Actor
open Actors

[<EntryPoint>]
let main argv =
    let winTailConfig = Configuration.defaultConfig()
    use winTailSystem = System.create "WinTailSystem" winTailConfig

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


    Console.WriteLine "Enter the path to the file you want to tail:"
    
    consoleReaderRef <! Messages.ReadInput

    winTailSystem.WhenTerminated.Wait()    
        
    0 // return an integer exit code
