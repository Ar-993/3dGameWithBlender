using _3DLight.Assets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace _3DLight.Models
{
    internal static class VegetationMaterials
    {
        private static readonly HashSet<string> Materials = new(StringComparer.OrdinalIgnoreCase)
        {
            "Bush",
            "Tree",
            "Tree_01",
            "Background",
            "Background_01"
        };

        public static bool UseAlphaCutOut(MeshData mesh)
        {
            string textureName = Path.GetFileNameWithoutExtension(mesh.TextureName);

            if (Materials.Contains(textureName))
            {  
                return true; 
            }
            if (Materials.Contains(mesh.MaterialName))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
