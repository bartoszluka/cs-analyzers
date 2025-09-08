# Unused variables analyzer

## What is this?

This is a C\# code analyzer to warn you about unused variables.
You can add it to your project, run `dotnet build` and see the warnings.

## Why?

This project was created specifically because the default behavior of the compiler skips over unused variables.
You might say that that's not true, that you've seen `unused variable` warnings before.

Yes, it exists, but unfortunately it's pretty limited.
Specifically, the right hand side must be a compile time constant.

So:

```cs
public void Foo()
{
    var x = "unused"; // this is detected
    var y = new System.Text.StringBuilder(); // this is NOT detected
}
```

So basically it only catches the simplest case.
In other words: it's useless.

This project is meant to fix this.

## How to use it?

There are 2 main ways of installing and using this analyzer, depending on what type of project you're working on:

- the simple, single project one
- central package management with `.props` files

choose the approach that suits you best.

### Add to a project

This project is on [NuGet](https://www.nuget.org/packages/MissingAnalyzers), so you can add it through `dotnet` CLI:

```sh
dotnet add package MissingAnalyzers
```

or add this to your `.csproj` file:

```xml
<ItemGroup>
  <PackageReference Include="MissingAnalyzers" Version="0.4.0" />
</ItemGroup>
```

### Add to all projects

If you're using "central package version management"
you can add this to the `Directory.Packages.props` file

```xml
<ItemGroup>
  <PackageVersion Include="MissingAnalyzers" Version="0.4.0" />
</ItemGroup>
```

and this to `Directory.Build.props` file

```xml
<ItemGroup>
  <PackageReference Include="MissingAnalyzers" />
</ItemGroup>
```

### What's next?

- [x] catch unused variables and report a compiler warning
- [x] publish the package on NuGet
- [x] fill `AnalyzerReleases.Shipped.md` (whatever those are)
- [x] maybe support versions older than `net8.0`? `netstandard2.0` supported
- [x] fix a bug with shadowing names in lambdas
- [ ] add tests
- [ ] add another analyzer if I see another problem
