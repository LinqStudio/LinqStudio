## Settings
This folder contains classes related to the user settings.
These settings are configurable by the user when the application is running.

> ⚠️ **Warning:** These are not the application settings. Application settings are still in the normal `appsettings.json` and `appsettings.development.json`

There are no global class that contains all the settings. This is voluntary to keep the settings modular and easier to maintain.
Furthermore, a single settings class would force the application to load all settings even if some of them are not used.

Each settings must implement `IUserSettingsSection`, this will automatically add the settings to the dependency injection.