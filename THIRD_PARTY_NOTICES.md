# Third-party components

The application uses the following components. It does not bundle Codex. The self-contained Windows installer also includes the .NET Core and Windows Desktop runtimes under the .NET Foundation MIT license.

- Microsoft.Data.Sqlite / Microsoft.Data.Sqlite.Core 10.0.10 — Microsoft, MIT License. https://github.com/dotnet/efcore
- SQLitePCLRaw bundle 3.0.5 and its transitive managed providers — Eric Sink and contributors, Apache License 2.0. https://github.com/ericsink/SQLitePCL.raw
- SQLite native library supplied by the SQLite package 3.53.4 — SQLite authors, public domain. https://www.sqlite.org/copyright.html
- .NET Core and Windows Desktop runtimes 10.0.12 — .NET Foundation and contributors, MIT License. https://github.com/dotnet/runtime and https://github.com/dotnet/wpf

The package license texts are included in the `licenses` directory of the binary distribution. Component versions and dependency information are also recorded in `QuotaFloat.deps.json`.
