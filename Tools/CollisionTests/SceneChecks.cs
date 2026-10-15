using _3DLight.Scenes;
using Microsoft.Xna.Framework;

internal static class SceneChecks
{
    public static void Run(Action<string, Action> run)
    {
        run("scene requests defer construction until Update and the last request wins", () =>
        {
            List<string> events = [];
            using SceneManager manager = new();
            manager.ChangeScene(() => Create("unused", events));
            manager.ChangeScene(() => Create("active", events));
            Require(manager.CurrentScene is null, "A scene entered before Update");
            manager.Draw(new GameTime());
            Expect(events);

            manager.Update(new GameTime());
            Expect(events, "active.create", "active.load", "active.enter", "active.update");
            Require(manager.CurrentScene is FakeScene { Name: "active" }, "The last request was not selected");
        });

        run("scene replacement releases the old scene before creating and entering the next", () =>
        {
            List<string> events = [];
            using SceneManager manager = new();
            manager.ChangeScene(() => Create("old", events));
            manager.Update(new GameTime());
            events.Clear();

            manager.ChangeScene(() => Create("new", events));
            manager.Draw(new GameTime());
            Expect(events, "old.draw");
            events.Clear();
            manager.Update(new GameTime());
            manager.Draw(new GameTime());
            Expect(events, "old.exit", "old.dispose", "new.create", "new.load", "new.enter",
                "new.update", "new.draw");
        });

        run("a scene can request replacement during Update without interrupting its frame", () =>
        {
            List<string> events = [];
            using SceneManager manager = new();
            FakeScene old = new("old", events);
            old.OnUpdate = () => manager.ChangeScene(() => Create("new", events));
            manager.ChangeScene(() => old);
            manager.Update(new GameTime());
            manager.Draw(new GameTime());
            Expect(events, "old.load", "old.enter", "old.update", "old.draw");
            Require(ReferenceEquals(manager.CurrentScene, old), "Update replaced its own scene immediately");

            events.Clear();
            manager.Update(new GameTime());
            Expect(events, "old.exit", "old.dispose", "new.create", "new.load", "new.enter", "new.update");
        });

        run("a scene can request replacement during Draw without changing later draws in the frame", () =>
        {
            List<string> events = [];
            using SceneManager manager = new();
            FakeScene old = new("old", events);
            old.OnDraw = () => manager.ChangeScene(() => Create("new", events));
            manager.ChangeScene(() => old);
            manager.Update(new GameTime());
            events.Clear();
            manager.Draw(new GameTime());
            manager.Draw(new GameTime());
            Expect(events, "old.draw", "old.draw");
            Require(ReferenceEquals(manager.CurrentScene, old), "Draw replaced its own scene immediately");

            events.Clear();
            manager.Update(new GameTime());
            Expect(events, "old.exit", "old.dispose", "new.create", "new.load", "new.enter", "new.update");
        });

        run("a replacement requested during Enter waits for the following Update", () =>
        {
            List<string> events = [];
            using SceneManager manager = new();
            FakeScene first = new("first", events);
            first.OnEnter = () => manager.ChangeScene(() => Create("second", events));
            manager.ChangeScene(() => first);
            manager.Update(new GameTime());
            Expect(events, "first.load", "first.enter", "first.update");

            events.Clear();
            manager.Update(new GameTime());
            Expect(events, "first.exit", "first.dispose", "second.create", "second.load",
                "second.enter", "second.update");
        });

        run("a failed scene load disposes the partial scene without entering or exiting it", () =>
        {
            List<string> events = [];
            using SceneManager manager = new();
            manager.ChangeScene(() => Create("old", events));
            manager.Update(new GameTime());
            events.Clear();
            InvalidOperationException failure = new("load failed");
            manager.ChangeScene(() => new FakeScene("broken", events) { OnLoad = () => throw failure });

            Require(ReferenceEquals(Catch(() => manager.Update(new GameTime())), failure),
                "The original load error was replaced");
            Expect(events, "old.exit", "old.dispose", "broken.load", "broken.dispose");
            Require(manager.CurrentScene is null, "A failed scene remained current");
            events.Clear();
            manager.Update(new GameTime());
            manager.Draw(new GameTime());
            Expect(events);
        });

        run("a failed scene Enter exits and disposes the partial scene", () =>
        {
            List<string> events = [];
            using SceneManager manager = new();
            InvalidOperationException failure = new("enter failed");
            manager.ChangeScene(() => new FakeScene("broken", events) { OnEnter = () => throw failure });

            Require(ReferenceEquals(Catch(() => manager.Update(new GameTime())), failure),
                "The original Enter error was replaced");
            Expect(events, "broken.load", "broken.enter", "broken.exit", "broken.dispose");
            Require(manager.CurrentScene is null, "An Enter failure published the partial scene");

            events.Clear();
            manager.ChangeScene(() => Create("recovered", events));
            manager.Update(new GameTime());
            Expect(events, "recovered.create", "recovered.load", "recovered.enter", "recovered.update");
        });

        run("an old scene Exit failure still disposes it and prevents constructing the next", () =>
        {
            List<string> events = [];
            using SceneManager manager = new();
            InvalidOperationException failure = new("exit failed");
            manager.ChangeScene(() => new FakeScene("old", events) { OnExit = () => throw failure });
            manager.Update(new GameTime());
            events.Clear();
            manager.ChangeScene(() => Create("new", events));

            Require(ReferenceEquals(Catch(() => manager.Update(new GameTime())), failure),
                "The old scene Exit error was replaced");
            Expect(events, "old.exit", "old.dispose");
            Require(manager.CurrentScene is null, "A disposed scene remained current");
        });

        run("scene factory failures occur after releasing the old scene and propagate", () =>
        {
            List<string> events = [];
            using SceneManager manager = new();
            manager.ChangeScene(() => Create("old", events));
            manager.Update(new GameTime());
            events.Clear();
            InvalidOperationException failure = new("factory failed");
            manager.ChangeScene(() => throw failure);

            Require(ReferenceEquals(Catch(() => manager.Update(new GameTime())), failure),
                "The original factory error was replaced");
            Expect(events, "old.exit", "old.dispose");
            Require(manager.CurrentScene is null, "Factory failure retained a disposed scene");
        });

        run("scene startup cleanup preserves errors from Enter Exit and Dispose", () =>
        {
            List<string> events = [];
            using SceneManager manager = new();
            InvalidOperationException enterFailure = new("enter failed");
            InvalidOperationException exitFailure = new("exit failed");
            InvalidOperationException disposeFailure = new("dispose failed");
            manager.ChangeScene(() => new FakeScene("broken", events)
            {
                OnEnter = () => throw enterFailure,
                OnExit = () => throw exitFailure,
                OnDispose = () => throw disposeFailure
            });

            Exception error = Catch(() => manager.Update(new GameTime()));
            Require(error is AggregateException aggregate && aggregate.Flatten().InnerExceptions
                .SequenceEqual(new Exception[] { enterFailure, exitFailure, disposeFailure }),
                "Startup cleanup lost one of the lifecycle errors");
            Expect(events, "broken.load", "broken.enter", "broken.exit", "broken.dispose");
            Require(manager.CurrentScene is null, "A broken scene remained current after cleanup");
        });

        run("scene load and disposal errors both reach the caller", () =>
        {
            List<string> events = [];
            using SceneManager manager = new();
            InvalidOperationException loadFailure = new("load failed");
            InvalidOperationException disposeFailure = new("dispose failed");
            manager.ChangeScene(() => new FakeScene("broken", events)
            {
                OnLoad = () => throw loadFailure,
                OnDispose = () => throw disposeFailure
            });

            Exception error = Catch(() => manager.Update(new GameTime()));
            Require(error is AggregateException aggregate && aggregate.Flatten().InnerExceptions
                .SequenceEqual(new Exception[] { loadFailure, disposeFailure }),
                "Failed-load cleanup lost the load or disposal error");
            Expect(events, "broken.load", "broken.dispose");
        });

        run("disposing the scene manager cancels pending creation and releases its scene once", () =>
        {
            List<string> events = [];
            SceneManager manager = new();
            manager.ChangeScene(() => Create("active", events));
            manager.Update(new GameTime());
            events.Clear();
            manager.ChangeScene(() => Create("pending", events));
            manager.Dispose();
            manager.Dispose();
            Expect(events, "active.exit", "active.dispose");
            CheckDisposed(manager);
        });

        run("disposing before the first Update does not construct a pending scene", () =>
        {
            List<string> events = [];
            SceneManager manager = new();
            manager.ChangeScene(() => Create("pending", events));
            manager.Dispose();
            manager.Dispose();
            Expect(events);
            CheckDisposed(manager);
        });

        run("scene manager disposal is final even when Exit and Dispose both fail", () =>
        {
            List<string> events = [];
            SceneManager manager = new();
            InvalidOperationException exitFailure = new("exit failed");
            InvalidOperationException disposeFailure = new("dispose failed");
            manager.ChangeScene(() => new FakeScene("broken", events)
            {
                OnExit = () => throw exitFailure,
                OnDispose = () => throw disposeFailure
            });
            manager.Update(new GameTime());
            events.Clear();
            manager.ChangeScene(() => Create("pending", events));

            Exception error = Catch(manager.Dispose);
            Require(error is AggregateException aggregate && aggregate.InnerExceptions
                .SequenceEqual(new Exception[] { exitFailure, disposeFailure }),
                "Manager disposal lost one of its cleanup errors");
            manager.Dispose();
            Expect(events, "broken.exit", "broken.dispose");
            CheckDisposed(manager);
        });
    }

    private static FakeScene Create(string name, List<string> events)
    {
        events.Add(name + ".create");
        return new FakeScene(name, events);
    }

    private static void CheckDisposed(SceneManager manager)
    {
        Require(Catch(() => manager.Update(new GameTime())) is ObjectDisposedException,
            "Update accepted a disposed manager");
        Require(Catch(() => manager.Draw(new GameTime())) is ObjectDisposedException,
            "Draw accepted a disposed manager");
        Require(Catch(() => manager.ChangeScene(() => throw new Exception("must not run")))
            is ObjectDisposedException, "ChangeScene accepted a disposed manager");
        Require(Catch(() => { _ = manager.CurrentScene; }) is ObjectDisposedException,
            "CurrentScene accepted a disposed manager");
    }

    private static Exception Catch(Action action)
    {
        try { action(); }
        catch (Exception error) { return error; }
        throw new InvalidOperationException("Expected an exception");
    }

    private static void Expect(List<string> events, params string[] expected) =>
        Require(events.SequenceEqual(expected),
            $"Expected [{string.Join(", ", expected)}], got [{string.Join(", ", events)}]");

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class FakeScene(string name, List<string> events) : IGameScene
    {
        public string Name => name;
        public Action? OnLoad { get; init; }
        public Action? OnEnter { get; set; }
        public Action? OnExit { get; init; }
        public Action? OnUpdate { get; set; }
        public Action? OnDraw { get; set; }
        public Action? OnDispose { get; init; }

        public void LoadContent() => Record("load", OnLoad);
        public void Enter() => Record("enter", OnEnter);
        public void Exit() => Record("exit", OnExit);
        public void Update(GameTime gameTime) => Record("update", OnUpdate);
        public void Draw(GameTime gameTime) => Record("draw", OnDraw);
        public void Dispose() => Record("dispose", OnDispose);

        private void Record(string action, Action? callback)
        {
            events.Add(name + "." + action);
            callback?.Invoke();
        }
    }
}
