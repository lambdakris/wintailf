module Program

open System
open Akkling
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
        consoleWriter
        |> actorOf2 
        |> props 
        |> spawn winTailSystem "consoleWriter"

    let inputValidatorRef =
        inputValidator consoleWriterRef
        |> actorOf2
        |> props
        |> spawn winTailSystem "inputValidator"    

    let consoleReaderRef = 
        consoleReader inputValidatorRef
        |> actorOf2 
        |> props 
        |> spawn winTailSystem "consoleReader"

    printInstructions() 

    consoleReaderRef <! ReadInput

    winTailSystem.WhenTerminated.Wait()    
        
    0 // return an integer exit code
