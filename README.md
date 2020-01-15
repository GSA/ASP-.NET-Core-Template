# ASP .NET Core Project Template
ASP .NET Core Project Template for GSA FM IT team.  Every one should be able to use this templae to build a ASP .NET Core web app with PostgreSQL database and beautifully looking U.S. Web Deisgn Standard UI. 

### Key Functions 
1. Landing page design with [U.S. Web Deisgn System 2](https://designsystem.digital.gov).
2. Cross-platform.
3. Sing-in pgae exmaple.


## Getting Started
These instructions will get you a copy of the project up and running on your local machine for development and testing purposes. See deployment for notes on how to deploy the project on a live system.

### Prerequisites
- Visual Studio 2017 or higher 
- .NET Core SDK 2.1.x 
- Entity Framework Core 2.1
- Npgsql.EntityFrameworkCore.PostgreSQL 2.1.1.1 (https://www.nuget.org/packages/Npgsql.EntityFrameworkCore.PostgreSQL/)

### Installing
1. Download and copy to \My Documents\Visual Studio 2017\Templates\ProjectTemplates folder.

### Using the Template for a New Project
1. Launch Visual Studio 2019 and create a new project. Use the "ASP.NET Core Web Application" template (C#). Use a unique name for the new project.
2. In the "Create a new ASP.NET Core web application" window, select ".NET Core" and "ASP.NET Core 3.1" in the dropdown menus. Choose the "Empty" project type and Check the "Configure for HTTPS" box. Click "Create".
3. Once the project has been created, close Visual Studio.
4. Clone this repository to a new folder.
5. Remove *all* of the files and folders from within your project's web root. This includes the following:
    - *bin/*
    - *obj/*
    - *Properties/*
    - *appsettings.Development.json*
    - *appsettings.json*
    - *Program.cs*
    - *Startup.cs*
6. Copy *all* files and folders from this repository's **2nd** *ASP Core MVC Template* subdirectory and paste them into the same location within your own project. You're replacing all of the files you removed in the previous step.
7. Rename the *ASP Core MVC Template.csproj* file to your own project's name. Edit it with an app like *Notepad* and make the following changes:
    - Replace the *<RootNamespace>* value with your project's namespace.
8. Rename the *ASP Core MVC Template.csproj.user* file to your own project's name.
9. *to be continued*
