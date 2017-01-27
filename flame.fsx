#load "html.fsx"
#r "./packages/FSharpx.Extras/lib/net45/FSharpx.Extras.dll"

open System
open System.IO
open FSharpx.String

type Context<'a> = {
    SiteName : string
    SiteUrl : string
    Payload : 'a

    /// Root of the source folder
    SourceRoot : string

    /// Root of the drop folder
    DropRoot : string
}
with
    static member Wrap (ctx: Context<'a>) (payload: 'b) =
        { SiteName = ctx.SiteName
          SiteUrl = ctx.SiteUrl
          SourceRoot = ctx.SourceRoot
          DropRoot = ctx.DropRoot
          Payload = payload }

type ContentType =
    | NoContent
    /// from text content
    | Text of string 
    /// from file path
    | File of string 
    /// from a stream
    | Stream of Stream

type Doc = {
    /// from front matter
    Metadata : Map<string, string> option 
    /// Content of the doc
    Content : ContentType
    /// path to the source of this doc
    Source : string 
    /// where to write to
    Destination : string 

    /// The main doc source ID for an asset group._AppDomain
    /// "" or null means there's no MainDoc for current doc
    MainDoc : string
}
with
    static member FromPath (fn: string) =
        { Metadata = None; Content = NoContent; Source = fn; Destination = ""; MainDoc = "" }
    
    static member FromPathWithMain fn mainDoc = { Doc.FromPath fn with MainDoc = mainDoc }

/// Process a doc and optionally exclude the doc from been generated
type DocProcessor = Context<Doc> -> Context<Doc> option

/// Process a group of docs
type Processor = Context<Doc list> -> Context<Doc list>

/// Crawl docs
type DocCrawler = Context<unit> -> Doc list

module ops =

    let splash (ctx: Context<Doc list>) : Context<Doc> list = ctx.Payload |> List.map (Context.Wrap ctx)
    let collect ctx (docs: Context<Doc> list) = docs |> List.map (fun d -> d.Payload) |> Context.Wrap ctx

    let (>=>) (f: 'a -> 'b option) (g: 'b -> 'c option) = 
        fun a ->
            match f a with
            | None -> None
            | Some b -> g b

    let group (docp : DocProcessor) : Processor =
        fun ctx ->
            ctx |> splash |> List.choose (docp) |> collect ctx

module Common =
    open System.Web

    /// <summary>
    /// http://stackoverflow.com/a/340454/695964
    /// Creates a relative path from one file or folder to another.
    /// </summary>
    /// <param name="fromPath">Contains the directory that defines the start of the relative path.</param>
    /// <param name="toPath">Contains the path that defines the endpoint of the relative path.</param>
    /// <returns>The relative path from the start directory to the end path or <c>toPath</c> if the paths are not related.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="UriFormatException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    let _MakeRelativePath (fromPath: string) (toPath: string) : string =
        if (String.IsNullOrEmpty(fromPath)) then failwith "fromPath"
        if (String.IsNullOrEmpty(toPath)) then failwith "toPath"

        let fromUri = new Uri(fromPath);
        let toUri = new Uri(toPath);

        if (fromUri.Scheme <> toUri.Scheme) then toPath // path can't be made relative.
        else
            let relativeUri = fromUri.MakeRelativeUri(toUri)
            let mutable relativePath = Uri.UnescapeDataString(relativeUri.ToString())

            if (toUri.Scheme.Equals("file", StringComparison.InvariantCultureIgnoreCase)) then
                relativePath <- relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)

            relativePath

    let ensureDir dir = 
        if Directory.Exists dir && not <| dir.EndsWith (Path.DirectorySeparatorChar.ToString()) then 
            dir + Path.DirectorySeparatorChar.ToString()
        else dir

    let MakeRelativePath base_ target =
        let base_, target = ensureDir <| Path.GetFullPath base_, 
                            ensureDir <| Path.GetFullPath target
        _MakeRelativePath base_ target

module Filter =
    open ops
        
    let exclude (ex: Context<Doc> -> bool) (ctx: Context<Doc>) =
        if ex ctx then None
        else Some ctx
    
    let excludeDirectory ctx = Directory.Exists ctx.Payload.Source
    let excludeAbsentFile ctx = File.Exists ctx.Payload.Source |> not
    let excludeDirSegment (ex: string -> bool) ctx = 
        let dir = ctx.Payload.Source |> Common.MakeRelativePath ctx.SourceRoot |> Path.GetDirectoryName
        let dirs = dir.Split(Path.DirectorySeparatorChar)
        match dirs |> Seq.tryFind ex with
        | None -> false
        | _ -> true
    
    let excludeRegex pattern (value:string) =
        System.Text.RegularExpressions.Regex.IsMatch(value, pattern)

    let filterDefault = 
        exclude excludeDirectory
        >=> exclude excludeAbsentFile
        // >=> (excludeRegex @"\..*" |> excludeDirSegment |> exclude)
        // >=> (excludeRegex @"_.*" |> excludeDirSegment |> exclude)

    /// Allow configuration? 
    let excludeDir (seg: string) =
        if seg.StartsWith("_") then true
        elif seg.StartsWith(".") then true
        else false

    let filterDirName (ctx: Context<Doc>) =
        let dir = ctx.Payload.Source |> Path.GetFullPath |> Path.GetDirectoryName 
        let dirs = dir.Split(Path.DirectorySeparatorChar)
        match dirs |> Seq.tryFind excludeDir with
        | None -> None
        | _ -> ctx |> Some

module Render =

    let passthru : DocProcessor =
        fun ctx -> 
            let doc = { ctx.Payload with Content = File <| ctx.Payload.Source }
            { ctx with Payload = doc } |> Some

module Router = 
    open Common

    let copy (ctx: Context<Doc>) =
        let relativeFilePath = MakeRelativePath ctx.SourceRoot ctx.Payload.Source
        { ctx with Payload = { ctx.Payload with Destination = Path.Combine(ctx.DropRoot, relativeFilePath) } } |> Some

/// writes doc onto disk
module Writer =
    /// Prints a doc
    /// TODO: it doesn't really writes so far
    let display ctx =
        printfn "%A" ctx
        ctx |> Some

module Crawler =
    /// crawl with asset group feature
    let rec _crawl (ctx: Context<unit>) (dir: string) (mainDoc : string) : Context<Doc> seq =
        seq {
            printfn "DEBUG: crawl directory: %A" dir
            let files = Directory.GetFiles(dir, "*")
            let index = files |> Array.tryFind (fun fn -> Path.GetFileNameWithoutExtension(fn) |> toLower = "index") 
            let mainDoc = 
                if Option.isSome index && mainDoc |> String.IsNullOrEmpty 
                then index.Value
                else mainDoc
            yield! files |> Seq.map (fun fn -> Doc.FromPathWithMain fn mainDoc |> Context.Wrap ctx)

            yield! Directory.GetDirectories dir
                   |> Seq.collect (fun dir -> _crawl ctx dir mainDoc)
        }

    let crawl ctx = _crawl ctx ctx.SourceRoot "" |> List.ofSeq |> ops.collect ctx

open ops

let processDoc = 
    Filter.filterDefault 
    >=> Router.copy 
    >=> Render.passthru 
    >=> Writer.display
    |> group

let final = Crawler.crawl >> processDoc

let ctx = {
    SiteName = "foo bar"
    SiteUrl = "http://foobar.com"
    Payload = ()
    SourceRoot = @"C:\Users\user\work\blog\source\_posts"
    DropRoot = @"c:\"
}

final ctx