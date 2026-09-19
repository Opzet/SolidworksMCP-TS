# WinFormsMudBlazor

This is a Visual Studio 2022 template that creates a C# project that embeds a minimal .NET 9.x Blazor application using <a href="https://mudblazor.com/" target="_blank">MudBlazor</a> in a C# Winform.

## Features

1. Targets .NET 9.x. Based on this Microsoft article: <a href="https://learn.microsoft.com/en-us/aspnet/core/blazor/hybrid/tutorials/windows-forms?view=aspnetcore-9.0" target="_blank">Build a Windows Forms Blazor app</a>
2. Provides some useful reusable razor components:
   * Full-screen toggle button: **FullScreenToggleButton.razor**.
   * Theme dark/light mode toggle button: **DarkModeToggleButton.razor**.
   * Sample screens à la MudBlazor `Blazor Web App` template provided <a href="https://mudblazor.com/getting-started/installation#using-templates" target="_blank">here</a>. 

# Importing WinFormsMudBlazor Project Template in Visual Studio 2022

You can import the WinFormsMudBlazor project template (`WinFormsMudBlazor.zip` file) into Visual Studio using one of the following methods:

---

## Option 1: Per-User Installation (Recommended)

1. **Locate the template**

   If you have cloned this repository, or downloaded it as a `.zip` file, built the source and exported the project as a template, you will find the created `WinFormsMudBlazor.zip` file here:
   ```
   %USERPROFILE%\Documents\Visual Studio 2022\My Exported Templates
   ```
   **- OR -**
   
   Find it here in `Releases`:
   
   <a href="https://github.com/Businessware/WinFormsMudBlazor/releases/tag/v1.0" target="_blank">WinFormsMudBlazor v1.0</a>

2. **Copy the `WinFormsMudBlazor.zip` file**.

3. **Paste it into this folder**:
   ```
   %USERPROFILE%\Documents\Visual Studio 2022\Templates\ProjectTemplates
   ```

4. **Restart Visual Studio 2022** (if it's open).

5. Open **File > New > Project**  
   - Search for your template by name, or  
   - Filter by `Language: C#`, and `Project Type: Custom` or `All project types`.

---

## Option 2: System-Wide Installation (All Users - Advanced)

1. **Copy your template `.zip` file** to:
   ```
   C:\Program Files\Microsoft Visual Studio\2022\<Edition>\Common7\IDE\ProjectTemplates
   ```
   > Replace `<Edition>` with `Community`, `Professional`, or `Enterprise`.

2. **Run Visual Studio as Administrator** once after copying to register the template.

> ⚠️ Note: Templates added this way may be overwritten during Visual Studio updates.

---

# License

Totally gratis - no license, use as you like!
