Milestone 1 (completed 4/1/22)

- [x] Arrow Functions

- [x] ** operator for pow

- [x] If (curly brackets implemented, need single statement with semicolon variant)

- [x] For Loops - iterators. (Need to see how the table without pairs call works with the MoonSharp default iterator)

- [x] For Loops - C style with conditions. (New parser concept).

- [x] do/while loops (try and make not conflict with lua’s do-end scope blocks).

- [x] While loops

- [x] Repeat loops

- [x] Functions with curly bracket { } syntax 

- [x] Single-line comments

- [x] Multiline comments

- [x] let/var alias for local

- [x] Tables using ‘:’

- [x] String literal keys for table init syntax

- [x] Square bracket list syntax 

- [x] Ternary Expression (CLike only)

- [x] Unary ! negation (TBC)

- [x] && (alias for and)

- [x] || (alias for or)

- [x] Compound assignment *= += -= /= **= ..= %=

- [x] Overloaded + operator that tries to coerce to string if it isn’t a number

- [x] .. replaced with + operator

- [x] Label syntax (CLike only)

- [x] Increment/Decrement shorthand. CLike only, breaks Lua comments



Milestone 2 (started 4/4/22)

- [ ] Template literals `` (equivalent to $”” + multiline support in c#)

- [ ] Replace # with .length, free # for other uses

- [ ] Ditch Moonsharp’s | lambda | syntax, free pipe for bitwise & implement bitwise operators

- [ ] Nill coalescing operators
  
  [x] ??= (nill coalescing assignment)
  
  [ ] ?. (nill coalescing member access)
  
  [ ] ?[] (nill coalescing element access)
  
  [x] ?? (nill coalescing)
  
  [ ] ?! (inverse nill coalescing)

- [ ] “null” as alias for nill

- [ ] “this” as alias for self



Backlog

- try/catch

- class syntax, oop extensions

- razor-esque syntax, Moonsharp.Templating assembly. See `doc/razor_like_templating_proposal` for details

- declaration hoisting  (attempted in #7, failed with local/global interop)


