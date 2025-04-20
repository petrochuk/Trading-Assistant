# Copilot Instructions

## Project Overview
This project is a .NET 9 application named **TradingAssistant**. It is a Windows application that uses WinUI and integrates with Interactive Brokers for trading functionalities. The project is structured into multiple components, including `AppCore`, `InteractiveBrokers`, and `App`.

### Key Features
- **Dependency Injection**: Configured using `Microsoft.Extensions.DependencyInjection`.
- **Logging**: Uses Serilog for logging with configuration from `appsettings.json`.
- **WinUI**: The application uses WinUI for its user interface.
- **Interactive Brokers Integration**: Provides trading functionalities via `IBClient` and `IBWebSocket`.

---

## Guidelines for Using GitHub Copilot

### General Coding Practices
- Follow the existing coding style and conventions in the project.
- Use `Nullable` reference types as they are enabled across all projects.
- Ensure all new code is compatible with `.NET 9` and WinUI.
- Use dependency injection for adding new services or components.

### Logging
- Use the existing Serilog configuration for logging.
- Log important application events, errors, and warnings using the `ILogger` interface.

### Dependency Injection
- Register new services in the `InitializeDI` method in `App.xaml.cs`.
- Use `ServiceCollection` to add services and ensure they are resolved via the `AppCore.ServiceProvider`.

### Configuration
- Add new configuration settings to `appsettings.json` or `appsettings.Debug.json` as needed.
- Use `IConfiguration` to access configuration values.

### Testing
- Write unit tests in the `AppCore.Tests` project.
- Use MSTest for writing and running tests.
- Ensure new features are covered by appropriate test cases.

---

## Project Structure
- **App**: The main application project containing the entry point and UI components.
- **AppCore**: Contains core logic and shared utilities.
- **InteractiveBrokers**: Handles integration with Interactive Brokers.
- **AppCore.Tests**: Contains unit tests for the `AppCore` project.

---

## Copilot-Specific Instructions
1. **Code Suggestions**:
   - Suggest code that adheres to the existing project structure and conventions.
   - Use `Microsoft.Extensions.DependencyInjection` for service registration.
   - Use `Serilog` for logging.

2. **File Modifications**:
   - When modifying `.csproj` files, ensure compatibility with `.NET 9` and WinUI.
   - Add new dependencies as `PackageReference` in the appropriate `.csproj` file.

3. **Error Handling**:
   - Use structured logging for error messages.
   - Ensure proper disposal of resources (e.g., `IBClient`, `IBWebSocket`) to avoid memory leaks.

4. **UI Development**:
   - Use WinUI components for building the user interface.
   - Register new views in the `InitializeDI` method.

5. **Testing**:
   - Suggest unit tests for new features.
   - Ensure tests follow the MSTest framework conventions.

---

## Additional Notes
- The application uses `RollingFile` for logging. Ensure new log files are written to the correct directory.
- The `appsettings.json` file is the primary configuration file. Avoid hardcoding configuration values in the code.
- Use `#if DEBUG` directives for debug-specific code.

By following these instructions, GitHub Copilot can assist in maintaining consistency and quality across the project.
