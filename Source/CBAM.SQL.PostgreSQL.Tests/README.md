# CBAM.SQL.PostgreSQL.Tests

This project contains unit tests for CBAM.PostgreSQL.* projects, which provide API to communicate with PostgreSQL backend asynchronously.
The tests can be run directly from Visual Studio by pressing Ctrl-R, Ctrl-A, but they require configuration files in this folder.

Currently, two configuration files are required: ```test_config.json``` and ```test_config_ssl.json```.
Both files should be in the format described in [CBAM.SQL.PostgreSQL.Implementation](../CBAM.SQL.PostgreSQL.Implementation), adhering to the structure of the `PgSQLConnectionCreationInfoData`class.

Once these two files are provided with appropriate values (and corresponding PostgreSQL backends set up), the tests should run to completion without errors.