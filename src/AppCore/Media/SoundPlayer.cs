using AppCore.Interfaces;

namespace AppCore.Media;

public class SoundPlayer : ISoundPlayer
{
    private readonly string _basePath;
    private readonly Lock _lockObject = new();

    public SoundPlayer() {
        // Base path relative to executable
        _basePath = Path.Combine(AppContext.BaseDirectory, "Resources");
    }

    public void PlaySound(string soundName) {
        try 
        {
            var path = Path.Combine(_basePath, $"{soundName}.wav");
            if (!File.Exists(path))
                return; // Silently ignore missing sound.

#pragma warning disable CA1416 // Validate platform compatibility
            lock (_lockObject) // SoundPlayer is singleton and not thread-safe.
            { 
                using var player = new System.Media.SoundPlayer(path);
                player.SoundLocation = path;
                player.Play(); // Fire-and-forget (non-blocking).
            }
#pragma warning restore CA1416
        } catch {
            // Intentionally swallow: sound failures must not impact trading flow.
        }
    }
}
