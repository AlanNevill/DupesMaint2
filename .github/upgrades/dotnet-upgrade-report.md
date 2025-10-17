# .NET 10.0 Upgrade Report

## Project target framework modifications

| Project name          | Old Target Framework | New Target Framework | Commits                          |
|:----------------------|:--------------------:|:--------------------:|----------------------------------|
| DupesMaint2.csproj    | net9.0               | net10.0              | f19e951f                         |

## NuGet Packages

| Package Name                                           | Old Version | New Version                 | Commit ID                        |
|:-------------------------------------------------------|:-----------:|:---------------------------:|----------------------------------|
| Microsoft.Build.Tasks.Core                             |             | 17.15.0-preview-25277-114   | d8d1f5eb                         |
| Microsoft.Build.Utilities.Core                         |             | 17.15.0-preview-25277-114   | d8d1f5eb                         |
| Microsoft.EntityFrameworkCore                          | 9.0.9       | 10.0.0-rc.2.25502.107       | bdc7dcac                         |
| Microsoft.EntityFrameworkCore.Analyzers                | 9.0.9       | 10.0.0-rc.2.25502.107       | bdc7dcac                         |
| Microsoft.EntityFrameworkCore.SqlServer                | 9.0.9       | 10.0.0-rc.2.25502.107       | bdc7dcac                         |
| Microsoft.EntityFrameworkCore.Tools                    | 9.0.9       | 10.0.0-rc.2.25502.107       | bdc7dcac                         |
| Microsoft.Extensions.Configuration.EnvironmentVariables| 9.0.9       | 10.0.0-rc.2.25502.107       | bdc7dcac                         |
| Microsoft.Extensions.Configuration.Json                | 9.0.9       | 10.0.0-rc.2.25502.107       | bdc7dcac                         |
| Microsoft.Extensions.DependencyInjection               | 9.0.9       | 10.0.0-rc.2.25502.107       | bdc7dcac                         |
| Microsoft.Extensions.Hosting                           | 9.0.9       | 10.0.0-rc.2.25502.107       | bdc7dcac                         |
| Microsoft.Extensions.Hosting.Abstractions              | 9.0.9       | 10.0.0-rc.2.25502.107       | bdc7dcac                         |
| Microsoft.Extensions.Logging.Console                   | 9.0.9       | 10.0.0-rc.2.25502.107       | bdc7dcac                         |
| System.Configuration.ConfigurationManager              | 9.0.9       | 10.0.0-rc.2.25502.107       | bdc7dcac                         |

## All commits

| Commit ID | Description                                                                                                      |
|:----------|:-----------------------------------------------------------------------------------------------------------------|
| ac173b1f  | Commit upgrade plan                                                                                              |
| f19e951f  | Update DupesMaint2.csproj to target .NET 10.0                                                                    |
| bdc7dcac  | Update package versions in DupesMaint2.csproj                                                                    |
| d8d1f5eb  | Add MSBuild package references to DupesMaint2.csproj                                                             |

## Next steps

- Test your application thoroughly to ensure compatibility with .NET 10.0 preview
- Review any warnings from the build process
- Consider running your test suite to validate functionality
- Monitor for any breaking changes in .NET 10.0 as it progresses through preview releases