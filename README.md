# C\# analyzers

## what?

This is a C\# code analyzer to warn you about unused variables.
You can add it to your project, run `dotnet build` and see the warnings.

## why?

This project was created specifically because the default behavior of the compiler skips over unused variables.
You might say that that's not true, that you've seen `unused variable` warnings before.

Yes, it exists, but unfortunately it's pretty limited.
Specifically, the right hand side must be a compile time constant.

So basically it only catches the simplest case.
In other words: it's useless.

This project is meant to fix this.

## how to use?

TODO

### what's next?

- [x] catch unused variables and report a compiler warning
- [ ] publish the package on nuget
- [ ] fill `AnalyzerReleases.Shipped.md` (whatever those are)
- [ ] maybe support versions older than `net8.0`?
- [ ] add another analyzer if I see another problem
