using Microsoft.Xna.Framework;

namespace _3DLight.Scenes;

internal interface IGameScene : IDisposable
{
    void LoadContent();
    void Enter();
    void Exit();
    void Update(GameTime gameTime);
    void Draw(GameTime gameTime);
}
