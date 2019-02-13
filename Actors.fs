module Actors

open System
open Akkling

type ConsoleReaderMessage =
    | ReadInput

type InputValidatorMessage =
    | ValidateInput of String

type ConsoleWriterMessage =
    | WriteInfo of String
    | WriteError of String

let (|IsExit|IsContent|) (input: String) =
    if input.ToLower() = "exit" then IsExit else IsContent

let (|IsValid|IsInvalid|) (content: String) =
    match content.Length with
    | n when n > 0 && n % 2 = 0 -> IsValid
    | _ -> IsInvalid

let consoleWriter =
    fun (context: Actor<ConsoleWriterMessage>) (message: ConsoleWriterMessage) ->
        match message with
        | WriteInfo text -> 
            Console.ForegroundColor <- ConsoleColor.Green 
            Console.WriteLine text
        | WriteError text ->
            Console.ForegroundColor <- ConsoleColor.Red 
            Console.WriteLine text

        Console.ResetColor()
        
        ignored ()

let inputValidator (consoleWriter: IActorRef<ConsoleWriterMessage>) =
    fun (context: Actor<InputValidatorMessage>) (message: InputValidatorMessage) ->
        match message with
        | ValidateInput input ->
            match input with
            | IsValid -> 
                consoleWriter <! WriteInfo "You entered a valid number of characters"
            | IsInvalid ->
                consoleWriter <! WriteError "You entered an invalid number of characters"
            
            context.Sender() <! ReadInput

        ignored()

let consoleReader (inputValidator: IActorRef<InputValidatorMessage>) =
    fun (context: Actor<ConsoleReaderMessage>) (message: ConsoleReaderMessage) ->
        match message with
        | ReadInput ->
            let input = Console.ReadLine()

            match input with
            | IsExit -> 
                context.System.Terminate() |> ignore
            | IsContent ->
                inputValidator <! ValidateInput input

        ignored ()