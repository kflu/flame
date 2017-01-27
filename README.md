# flame

A functional static site generator framework

# TODO

- how to generate index page, tag page, etc. as your system is one-to-one transforming
    - create dummy page with front matter with `layout` for template to fill. [good]
- in-post tag plugin
    - `{ URL_for "file path" }`
    - maybe using [DotLiquid custom tag][liqtag]
- it looks like I still need a template engine (DotLiquid) and use it on any text content
- how to parse [Front Matter][liqfront]?

## Permalink, post assests

Allow post assests to work with relative links, without using tag plugin. Also should support permalink.

    source
    |_ dir1
      |_ hello.md
      |_ image1.png
    |_ dir2
      |_ foo1.md
      |_ foo2.md  // what to do??
      |_ image1.png


## Plugin system

Think about making it plugin based, e.g., everything is plugin. Think about how to dynamically load them, install them individually?

Maybe install each via Paket. Then in script:

    #load <...>
    // ...
    process1 >=> process2 >=> process_just_loaded >=> ...

## Configuration

Allow configuration via Yaml. Load it as `Map<string, string>` so that plugins can read too. Might need utilities to ease the use of `Map`. Maybe through `fsharpx`.

[liqtag]: https://github.com/dotliquid/dotliquid/wiki/DotLiquid-for-Developers#create-your-own-tags
[liqfront]: https://jekyllrb.com/docs/frontmatter/