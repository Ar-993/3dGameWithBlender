using System;
using _3DLight.Gameplay;
using _3DLight.Ui;
using Gum;
using Gum.DataTypes;
using Gum.Forms;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using BlendState = Microsoft.Xna.Framework.Graphics.BlendState;


namespace _3DLight.Scenes
{
    internal sealed class SceneMenu : IGameScene
{
        private readonly Game game;
        private readonly SceneManager scenes;
        private readonly Level level = new();
        private readonly SceneLighting lighting = new();
        private readonly GumService gum = GumService.Default;

        private readonly Vector3 cameraPosition = new(-3f, 4.6f, 17f);
        private readonly Vector3 cameraTarget = new(-3f, 3.8f, 8f);

        private ContentManager? content;
        private SpriteBatch spriteBatch = null!;
        private Texture2D pixel = null!;
        private Button playButton = null!;
        private Button exitButton = null!;
        private Matrix view;
        private bool gumStarted;
        private bool disposed;

        public SceneMenu(Game game, SceneManager scenes)
        {
            this.game = game;
            this.scenes = scenes;
        }

        public void LoadContent()
        {
            content = new ContentManager(
           game.Services,  
           game.Content.RootDirectory);

            level.LoadContent(content, LevelSession.ModelName);

            view = Matrix.CreateLookAt(
                cameraPosition,
                cameraTarget,
                Vector3.Up);

            lighting.Fog.Enabled = true;
            lighting.PointPosition = cameraPosition;

        }

        public void Enter()
        {
            game.IsMouseVisible = true;
            gumStarted = true;
            gum.Initialize(game, DefaultVisualsVersion.V3);
            spriteBatch = new SpriteBatch(game.GraphicsDevice);
            pixel = new Texture2D(game.GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });
            playButton = CreateButton(-36f, (_, _) =>
        scenes.ChangeScene(() => new GameplayScene(game)));

            exitButton = CreateButton(36f, (_, _) => game.Exit());

        }

        private static Button CreateButton(float offsetY, EventHandler onClick)
        {
            var button = new Button
            {
                Text = string.Empty,
                Width  = 240,
                Height = 60,
                WidthUnits = DimensionUnitType.Absolute,
                HeightUnits = DimensionUnitType.Absolute
            };
            button.Visual.AddToRoot();
            button.Anchor(Anchor.Center);
            button.Y = offsetY;
            button.Click += onClick;
            return button;
        }

        public void Update(GameTime gameTime)
        {
            gum.Update(gameTime);
        }

        public void Draw(GameTime gameTime)
        {
            DrawBackground();
            gum.Draw();
            DrawLabels();
        }

        private void DrawBackground()
        {
            GraphicsDevice graphics = game.GraphicsDevice;

            graphics.Clear(lighting.Fog.Color);
            graphics.BlendState = BlendState.Opaque;
            graphics.DepthStencilState = DepthStencilState.Default;
            graphics.RasterizerState = RasterizerState.CullNone;

            Matrix projection = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.ToRadians(70f),
                graphics.Viewport.AspectRatio,
                0.04f,
                1000f);

            level.Draw(view, projection, lighting);
        }

        private void DrawLabels()
        {
            spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone);

            DrawButtonText(playButton, "ИГРАТЬ");
            DrawButtonText(exitButton, "ВЫХОД");

            spriteBatch.End();
        }

        private void DrawButtonText(Button button, string text)
        {
            const int scale = 2;

            Vector2 size = PsxPixelFont.MeasureString(text, scale);

            Vector2 position = new(
                button.AbsoluteLeft + (button.ActualWidth - size.X) / 2f,
                button.AbsoluteTop + (button.ActualHeight - size.Y) / 2f);

            PsxPixelFont.Draw(
                spriteBatch, pixel, text,
                position + new Vector2(2f),
                new Color(0, 0, 0, 150),
                scale);

            PsxPixelFont.Draw(
                spriteBatch, pixel, text,
                position,
                new Color(230, 230, 230, 210),
                scale);

        }

        public void Exit()
        {

        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (gumStarted) gum.Uninitialize();
            spriteBatch?.Dispose();
            level.Dispose();
            content.Dispose();
        }
    }
}
