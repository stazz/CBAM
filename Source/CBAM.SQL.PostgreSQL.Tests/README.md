# CBAM.SQL.PostgreSQL.Tests

This project contains unit tests for CBAM.PostgreSQL.* projects, which provide API to communicate with PostgreSQL backend asynchronously.
The tests can be run directly from Visual Studio by pressing Ctrl-R, Ctrl-A, but they require configuration files in this folder.

Currently, two configuration files are required: ```test_config.json``` and ```test_config_ssl.json```.
Both files should be in the following format:
```
{
  "Host": "ip number here",
  "Port": 5432 or some other port,
  "Database": "the name of the database",
  "Username": "username to connect to the database",
  "Password": "password to use when connecting with provided username"
}
```

Once these two files are provided with appropriate values, the tests should run to completion without errors.