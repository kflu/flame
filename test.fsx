open System.IO
open System

Directory.GetFiles("./packages/FAKE") |> Array.map (fun fn -> Path.GetFileNameWithoutExtension(fn))