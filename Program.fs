module Program

open System
open Akkling
open Akka.Actor
open Actors

let printInstructions () =
    Console.WriteLine "Enter whatever you like into the console!"
    Console.Write "Some lines will appear as"
    Console.ForegroundColor <- ConsoleColor.Red
    Console.Write " red"
    Console.ResetColor ()
    Console.Write " and others will appear as"
    Console.ForegroundColor <- ConsoleColor.Green
    Console.Write " green! "
    Console.ResetColor ()
    Console.WriteLine ()
    Console.WriteLine ()
    Console.WriteLine "Type 'exit' to quit this application at any time.\n"

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
        Actors.inputValidator consoleWriterRef tailCoordinatorRef
        |> actorOf2
        |> props
        |> spawn winTailSystem "InputValidator"    

    let consoleReaderRef = 
        Actors.consoleReader inputValidatorRef
        |> actorOf2 
        |> props 
        |> spawn winTailSystem "ConsoleReader"

    printInstructions() 

    consoleReaderRef <! Messages.ReadInput

    winTailSystem.WhenTerminated.Wait()    
        
    0 // return an integer exit code
