using System.Runtime.ExceptionServices;
using Microsoft.Xna.Framework;

namespace _3DLight.Scenes;

internal sealed class SceneManager : IDisposable
{
    private IGameScene? currentScene;
    private Func<IGameScene>? pendingScene;
    private bool disposed;

    public IGameScene? CurrentScene
    {
        get
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return currentScene;
        }
    }

    public void ChangeScene(Func<IGameScene> createScene)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(createScene);
        // Создание откладываем: запрос из Update/Draw не разрушает текущую сцену.
        pendingScene = createScene;
    }

    public void Update(GameTime gameTime)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        Func<IGameScene>? createScene = pendingScene;
        pendingScene = null;
        if (createScene is not null)
            ApplyChange(createScene);

        currentScene?.Update(gameTime);
    }

    public void Draw(GameTime gameTime)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        currentScene?.Draw(gameTime);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        pendingScene = null;
        IGameScene? previousScene = currentScene;
        currentScene = null;
        if (previousScene is not null)
            ReleaseScene(previousScene, callExit: true);
    }

    private void ApplyChange(Func<IGameScene> createScene)
    {
        IGameScene? previousScene = currentScene;
        currentScene = null;
        // Gum глобален: старый интерфейс освобождаем до создания нового.
        if (previousScene is not null)
            ReleaseScene(previousScene, callExit: true);

        IGameScene nextScene = createScene()
            ?? throw new InvalidOperationException("Фабрика вернула пустую сцену.");
        bool entering = false;
        try
        {
            nextScene.LoadContent();
            entering = true;
            nextScene.Enter();
            currentScene = nextScene;
        }
        catch (Exception error)
        {
            try
            {
                ReleaseScene(nextScene, callExit: entering);
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException(
                    "Не удалось загрузить сцену и полностью освободить её ресурсы.",
                    error, cleanupError);
            }

            throw;
        }
    }

    private static void ReleaseScene(IGameScene scene, bool callExit)
    {
        Exception? exitError = null;
        if (callExit)
        {
            try { scene.Exit(); }
            catch (Exception error) { exitError = error; }
        }

        // Ошибка Exit не должна мешать освобождению ресурсов.
        try { scene.Dispose(); }
        catch (Exception disposeError)
        {
            if (exitError is not null)
                throw new AggregateException("Ошибки выхода из сцены и освобождения ресурсов.",
                    exitError, disposeError);
            throw;
        }

        if (exitError is not null)
            ExceptionDispatchInfo.Capture(exitError).Throw();
    }
}
