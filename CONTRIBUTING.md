# Contributing to Slant (寸度)

**Slant** is under active development — feedback, bug reports, new architectural window frame ideas, and Pull Requests are all very welcome.

欢迎提 Issue 反馈问题、分享新窗框或光影创意，以及直接提交 Pull Request，本项目会持续维护与更新。

## Ways to Contribute

- **Report bugs**: Open a [Bug Report](.github/ISSUE_TEMPLATE/bug_report.md) with your Windows version, display scaling, and steps to reproduce.
- **Suggest ideas**: Open a [Feature Request](.github/ISSUE_TEMPLATE/feature_request.md) for new window mullion styles, lighting/weather nuances, or UX improvements.
- **Submit a Pull Request**:
  1. Fork the repository and create a topic branch from `main`.
  2. Keep changes focused — core WebGL optics and weather simulation live in [`wallpaper/index.html`](wallpaper/index.html), while native Windows host / WinRT lock screen integration lives in [`src/`](src/).
  3. Before opening a PR, verify the Windows host builds and passes its built-in self-test:

```powershell
dotnet build src\ObliqueSeasons.csproj -c Release
.\ObliqueSeasons.exe --self-test
```

Thank you for helping make Slant better!
