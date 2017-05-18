# CBAM
Connection-Based Asynchronous Messaging (CBAM) framework provides asynchronous task-based API and implementation for workflow which communicates with e.g. SQL or LDAP processes.

## Portability
CBAM projects aim to be as portable as possible, most of them targeting .NET Standard 1.0.

# CBAM.Abstractions
This project provides interfaces and abstractions which are common for all connection-based async messaging, such as interacting with SQL database or LDAP server.

# CBAM.Abstractions.Implementation
This project has skeleton implementations for interfaces in CBAM.Abstractions.

# CBAM.Tabular
This project has provides interfaces which are suitable for retrieving data in tabular format (async-gettable rows with async-gettable column values).

# CBAM.Tabular.Implementation
This project has skeleton implementations for interfaces in CBAM.Tabular, and also uses CBAM.Abstractions.Implementation skeleton implementations in order to tie CBAM.Tabular with CBAM.Abstractions.

# CBAM.SQL
This project specializes and augments the interfaces of CBAM.Abstractions in such way that interacting with SQL databases would be easy and natural.
The CBAM.Tabular project is used to provide access to data rows returned by SQL database.

# CBAM.SQL.Implementation
This project provides skeleton implementations for interfaces in CBAM.SQL and uses the skeleton implementations provided in CBAM.Tabular.Implementation.

# CBAM.SQL.PostgreSQL
This project specializes and augments the interfaces of CBAM.SQL with PostgreSQL-specific features, such as notifications and type system.

# CBAM.SQL.PostgreSQL.Implementation
This project provides PostgreSQL-specific implementation for CBAM.SQL and CBAM.SQL.PostgreSQL projects, exposing API to create connection pools which result in PostgreSQL-specific connections.
PgSQLConnectionPool is a good class to start exploring this project.

# CBAM.SQL.PostgreSQL.JSON
This project provides extension methods to enable support for ```json``` and ```jsonb``` PostgreSQL types.
These extension methods are available for connection pools and connections.
The ```json``` and ```jsonb``` types will be directly deserialized to Newtonsoft.JSON ```JToken``` objects.

# CBAM.SQL.PostgreSQL.Tests
The test suite project for CBAM.SQL.PostgreSQL.* projects.
In order to run the tests, add ```test_config.json``` and ```test_config_ssl.json``` configuration files to the project directory.
These files will be used to create ```PgSQLConnectionCreationInfoData``` object required to connect to database.