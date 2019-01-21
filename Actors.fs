module Actors

open System
open Akkling

type Command =
    | Proceed
    | Content of String
    | Exit

let (|Exit|Content|) (input: String) =
    if input.ToLower() = "exit" then Exit else Content input

let (|Empty|Even|Odd|) (content: String) =
    match content.Length with
    | 0 -> Empty
    | n when n % 2 = 0 -> Even 
    | _ -> Odd

let printInColor (color: ConsoleColor) (content: String) =
    Console.ForegroundColor <- color
    Console.WriteLine(content)
    Console.ResetColor()

let consoleWriter =
    fun (context: Actor<String>) (message: String) ->
    match message with
    | Empty -> printInColor ConsoleColor.Yellow "You did not enter any characters"
    | Even -> printInColor ConsoleColor.Green "You entered an even number of characters"
    | Odd -> printInColor ConsoleColor.Red "You entered an odd number of characters"
    
    ignored ()

let consoleReader (consoleWriter: IActorRef<String>) =
    fun (context: Actor<Command>) (message: Command) ->
    let input = Console.ReadLine()

    match input with
    | Exit -> 
        context.System.Terminate() |> ignore
    | Content c ->
        consoleWriter <! input
        context.Self <! Proceed

    ignored ()