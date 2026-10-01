using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace _3DLight.Models
{
    internal readonly struct RuntimeVertex : IVertexType
    {
        //Vector2 — 2 числа: 8 байт.
        //Vector3 — 3 числа: 12 байт.
        //Vector4 — 4 числа: 16 байт.
        //Byte4 — 4 однобайтовых значения: 4 байта.
        public static readonly VertexDeclaration Declaration = new(
            new VertexElement(
                offset: 0,
                VertexElementFormat.Vector3,
                VertexElementUsage.Position,
                usageIndex: 0),
            new VertexElement(
                offset: 12,
                VertexElementFormat.Vector3,
                VertexElementUsage.Normal,
                usageIndex: 0),
            new VertexElement(
                offset: 24,
                VertexElementFormat.Vector2,
                VertexElementUsage.TextureCoordinate,
                usageIndex: 0),
            new VertexElement(
                offset: 32,
                VertexElementFormat.Byte4,
                VertexElementUsage.BlendIndices,
                usageIndex: 0),
            new VertexElement(
                offset: 36,
                VertexElementFormat.Vector4,
                VertexElementUsage.BlendWeight,
                usageIndex: 0));

        private readonly Vector3 position;
        private readonly Vector3 normal;
        private readonly Vector2 textureCoordinate;
        private readonly Byte4 boneIndices;
        private readonly Vector4 boneWeights;

        public RuntimeVertex(
            Vector3 position,
            Vector3 normal,
            Vector2 textureCoordinate,
            Byte4 boneIndices,
            Vector4 boneWeights)
        {
            this.position = position;
            this.normal = normal;
            this.textureCoordinate = textureCoordinate;
            this.boneIndices = boneIndices;
            this.boneWeights = boneWeights;
        }

        VertexDeclaration IVertexType.VertexDeclaration => Declaration;
    }
}
