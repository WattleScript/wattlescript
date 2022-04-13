<img src="https://user-images.githubusercontent.com/10167247/163087025-6098be0a-0d50-4228-b73a-d8ff661cedd9.svg" width="500" alt="WattleScript">

https://wattlescript.github.io/


<hr/>

WattleScript is a scripting engine written in C# for  runtimes supporting .NET Standard 2.0 and newer. (.NET 4.7.2+, .NET Core 3.1+). It is a dual-language environment, providing support for Lua 5.2 code as well as its own language, Wattle.

Using WattleScript is as easy as:

```cs
var script = new Script();
script.DoString("print('Hello World!')");
```

WattleScript is based off the tried and tested [MoonSharp](https://moonsharp.org) project, inheriting its VM design and test suite. The design focuses on easy and fast interop with .NET objects and functions.

### Features

* [Wattle](https://wattlescript.github.io/about_wattle) scripting language.
* Lua mode 99% compatible with Lua 5.2, differences documented [here](https://wattlescript.github.io/lua_differences).
* Easily configured sandbox for safe execution of untrusted scripts.
* Minimal garbage generation at runtime
* No external dependencies
* Easy and performant interop with CLR objects, with runtime code generation where supported
* Source Generator for AOT interop.
* Support for awaiting on returned values
* Supports dumping/loading bytecode
* Support for the complete Lua standard library with very few exceptions (mostly located in the `debug` module).
* `json` module for loading json into tables safely at runtime.

### License

WattleScript is licensed under the 3-clause BSD License. See [LICENSE](https://github.com/WattleScript/wattlescript/blob/main/LICENSE) for details.
