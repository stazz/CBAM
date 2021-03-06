# CBAM.SQL.MSBuild
This project contains one abstract task, `AbstractSQLConnectionUsingTask`, to be used as base class for other libraries, and `ExecuteSQLStatementsTask`, which can be used to execute SQL statements from file to the database.
The `ExecuteSQLStatementsTask` has a number of required and optional parameters, all of which are explained below.

TODO: This should probably be made into a [.NET Core Global Tool](https://natemcmaster.com/blog/2018/05/12/dotnet-global-tools/).

## ExecuteSQLStatementsTask mandatory parameters
* `PoolProviderPackageID` of type `String`: should specify the NuGet package ID of the package holding type implementing the `AsyncResourcePoolProvider` type from [UtilPack.ResourcePooling](https://github.com/CometaSolutions/UtilPack/tree/develop/Source/UtilPack.ResourcePooling) project. Currently there is only one such package: [CBAM.SQL.PostgreSQL.Implementation](../CBAM.SQL.PostgreSQL.Implementation).
* `PoolConfigurationFilePath` of type `String`: should specify the path to JSON file containing serialized configuration needed to access the database. If one is using `CBAM.SQL.PostgreSQL.Implementation` as value for `PoolProviderPackageID`, then [there is example of the format of the file](../CBAM.SQL.PostgreSQL.Implementation).
* `SQLFilePaths` of type `ITaskItem[]`: This parameter should hold the paths to all the files containing SQL statements that should be executed. Each file may have `Encoding` metadata to specify the encoding that should be used when reading text from the file.

## ExecuteSQLStatementsTask optional parameters
* `PoolProviderVersion` of type `String`: the version part paired with `PoolProviderPackageID`, should specify the version of the NuGet package holding type implementing `AsyncResourcePoolProvider` type from [UtilPack.ResourcePooling](https://github.com/CometaSolutions/UtilPack/tree/develop/Source/UtilPack.ResourcePooling) project. If not specified, newest version will be used.
* `PoolProviderAssemblyPath` of type `String`: the path within the NuGet package specified by `PoolProviderPackageID` and `PoolProviderVersion` parameters, where the assembly holding type implementing `AsyncResourcePoolProvider` type from [UtilPack.ResourcePooling](../UtilPack.ResourcePooling) project resides. Is used only for NuGet packages with more than one assembly in their framework-specific folder. It is not needed for [CBAM.SQL.PostgreSQL.Implementation](../CBAM.SQL.PostgreSQL.Implementation) package.
* `PoolProviderTypeName` of type `String`: once the assembly is loaded using `PoolProviderPackageID`, `PoolProviderVersion` and `PoolProviderAssemblyPath` parameters, this parameter may be used to specify the name of the type implementing `AsyncResourcePoolProvider` type from [UtilPack.ResourcePooling](../UtilPack.ResourcePooling) project. If left out, the first suitable type from all types defined in the assembly will be used. It is not needed for [CBAM.SQL.PostgreSQL.Implementation](../CBAM.SQL.PostgreSQL.Implementation) package.
* `DefaultFileEncoding` of type `String`: Specifies the default encoding to use when `Encoding` metadata is missing from a file path in `SQLFilePaths` parameter. Defaults to UTF-8, if left out.
* `RunSynchronously` of type `Boolean`: this is infrastructure-related parameter, and actually is always used, since the usage is in non-virtual method. This parameter, if `true`, will skip calling [Yield](https://docs.microsoft.com/en-us/dotnet/api/microsoft.build.framework.ibuildengine3.yield) method.
* `WhenExceptionInMultipleStatements` of enum type `CBAM.SQL.WhenExceptionInMultipleStatements`: this property tells how to react when an exception occurs in the middle of processing statements of the file. By default it is `Continue`, which just continues to execute next statements. Sometimes this can cause excessive error output, and `Rethrow` and `RollbackAndStartNew` values can be used to specify different behaviour.

# Example

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard1.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="UtilPack.NuGet.MSBuild" Version="2.0.0"/>
  </ItemGroup>
  
  <UsingTask
    Condition=" '$(UtilPackNuGetMSBuildAssemblyPath)' != '' "
    TaskFactory="UtilPack.NuGet.MSBuild.NuGetTaskRunnerFactory"
    AssemblyFile="$(UtilPackNuGetMSBuildAssemblyPath)"
    TaskName="CBAM.SQL.MSBuild.ExecuteSQLStatementsTask"
  >
    <Task>
      <NuGetTaskInfo>
        <PackageID>CBAM.SQL.MSBuild</PackageID>
        <PackageVersion>0.3.0</PackageVersion>
      </NuGetTaskInfo>
    </Task>
  </UsingTask>
  
  <Target Name="ExecuteSQL">
    <CBAM.SQL.MSBuild.ExecuteSQLStatementsTask
      ResourcePoolProviderPackageID="CBAM.SQL.PostgreSQL.Implementation"
      ResourcePoolProviderVersion="0.3.0"
      ResourceConfigurationFilePath="my/path/to/database_config.json"
      SQLFilePaths="my/path/to/sql_file.sql"
    />
    
  </Target>
</Project>
```

# Distribution
The [NuGet package](http://www.nuget.org/packages/UtilPack.NuGet.Deployment.MSBuild) has the same package ID as this folder name.
__The task provided by this project should be loaded using [UtilPack.NuGet.MSBuild](../UtilPack.NuGet.MSBuild) task factory.__
