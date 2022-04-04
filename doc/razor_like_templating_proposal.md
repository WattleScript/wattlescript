# Templating extension for CLike mode

When this extension is toggled everything is treated as plaintext and outputted to stdout by default. 

For transition from plaintext to CLike code `@` is used. Expressions are then evaluated and their results pushed to stdout.

To escape an `@` symbol, another `@` has to be used.

```cshtml
@@Hello
```

```html
@Hello
```

## Implicit expressions

Start with an `@` symbol and are followed by CLike code:

```cshtml
<div>Running for: @os.clock() seconds</div>
```

```html
<div>Running for: 3.1459 seconds</div>
```

Except for (meta)function calls & tables indexing, spaces cannot be included. This is an example of such an expression:

```razor
@{
    a = 10
}
<div>@a > 0 ? "yes" : "no"</div>
```

In this example only `@a` part would be considered as a CLike expression and stdout would be written as:

```html
<div>10 > 0 ? "yes" : "no"</div>
```

## Explicit expressions

To allow for expressions where spaces are required, explicit expressions exist. They consist of an `@` symbol followed by balanced parenthesis. To execute ternary expression above, the template would need to look like:

```razor
@{
    a = 10
}
<div>@(10 > 0 ? "yes" : "no")</div>
```

This would write to stdout:

```html
<div>yes</div>
```

*Any implicit expression can be written as an explicit expression, hence explicit expressions represent a superset of implicit expressions.*

## Encoding

By default results of expressions (the last item on stack after interpretation) are **not encoded** and written as-is.  A global table `Html` exists to control this behavior.

*`Html` might be realized as `[MoonSharpModule]` but needs to be declared as partial & public so user apps can extend its content.*

In default implementation, `Html` has following methods:

- `Raw` -> just a passtrough method

- `Encode` -> result is HTML encoded

```razor
@Html.Encode("<div>hello</div>")
```

```html
&lt;div&gt;hello&lt;/div&gt;
```

## Code blocks

Start with an `@` symbol and are enclosed by `{}`. Unlike expressions, code blocks do not render to stdout and are used for side effects / control only.

```
@{
    tbl = [10, 20, 30]
}
```

After executing this template, stdout would be empty.

An example of a code block with an arrow function declared and the said function called:

```razor
@{
    say = (what) => {
        <div>@what</div>
    }
}

@say("hello")
```

```html
<div>hello</div>
```

There are three ways to transition from a code block to plaintext:

1) transition via `<text></text>` tag:
   
   In this type of transition only content of the tag is written to stdout but not the tag itself.
   
   ```razor
   @{
       a = 10
       <text>
       Value of a is @a
       </text>
   }
   ```
   
   ```html
   Value of a is 10
   ```
2. transition via any other HTML tag:
   
   Behaves the same as transition via `<text></text>` but the tag itself is also written to stdout.
   
   ```razor
   @{
       a = 10
       <div>
       Value of a is @a
       </div>
   }
   ```
   
   ```html
   <div>Value of a is 10</div>
   ```

3. line transition:
   
   Renders rest of the current line as plaintext. To perform line transition `@:` is used.
   
   ```razor
   @{
       a = 10
       @:Value of a is @a
   }
   ```
   
   ```html
   Value of a is 10
   ```

## Keywords

1. `@if`, `elseif`, `else if`, `else`
   
   ```razor
   @if (10 > 5) {
       <div>10 gt 5</div>
   }
   ```
   
   ```html
   <div>10 gt 5</div>
   ```
   
   `elseif`, `else if` & `else` don't use `@` symbol.
   
   ```razor
   @if (2 > 1) {
       <div>2 > 1</div>
   }
   else {
       <div>2 < 1</div>
   }
   ```
   
   ```html
   <div>2 > 1</div>
   ```

2. `@for`, `@while`, `@do`
   
   ```razor
   <ul>
   @for (i in 1..3) {
       <li>@i</li>
   }
   </ul>
   ```
   
   ```html
   <ul>
       <li>1</li>
       <li>2</li>
       <li>3</li>
   </ul>
   ```
   
   ```razor
   @{
       i = 0
   }
   @do {
       <div>@counter</div>
       i++;
   } while (i < 3)
   ```
   
   ```html
   <div>0</div>
   <div>1</div>
   <div>2</div>
   ```
   
   ```razor
   @{
       i = 0
   }
   @while (i < 2) {
       <div>@i</div>
   }
   ```
   
   ```html
   <div>0</div>
   <div>1</div>
   <div>2</div>
   ```

3. `@require`
   
   ```razor
   @require "myModule"
   ```

4. `@function` 
   
   ```razor
   @function MyFunc(n) {
       <div>@n</div>
   }
   
   @MyFunc(10)
   ```
   
   ```html
   <div>10</div>
   ```