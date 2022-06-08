using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SELib;
using CallOfFile;
using System.Runtime;
using System.Numerics;

namespace SExCoD
{
    internal class XAnimLoader
    {
        /// <summary>
        /// Handles reading a <see cref="SEAnim"/> from an XANIM.
        /// </summary>
        /// <param name="filePath">The file to read from.</param>
        /// <param name="animModel">The anim model to use.</param>
        /// <returns>A <see cref="SEAnim"/> consumed from an XANIM.</returns>
        public static SEAnim Read(string filePath, SEModel animModel)
        {
            using var reader = TokenReader.CreateReader(filePath);
            return Read(reader, animModel);
        }

        /// <summary>
        /// Handles reading a <see cref="SEModel"/> from an XMODEL.
        /// </summary>
        /// <param name="reader">The reader to read from.</param>
        /// <param name="animModel">The anim model to use.</param>
        /// <returns>A <see cref="SEAnim"/> consumed from an XANIM.</returns>
        public static SEAnim Read(TokenReader reader, SEModel animModel)
        {
            var anim = new SEAnim();

            // First check that we have our version and anim identifiers.
            // We don't bother checking version, if the data is good, we good.
            reader.RequestNextTokenOfType<TokenData>("ANIMATION");
            reader.RequestNextTokenOfType<TokenDataUInt>("VERSION");

            var partCount = (int)reader.RequestNextTokenOfType<TokenDataUInt>("NUMPARTS").Value;
            var parts = new TokenDataUIntString[partCount];
            var indexMap = new int[animModel.Bones.Count];

            for (int i = 0; i < partCount; i++)
            {
                parts[i] = reader.RequestNextTokenOfType<TokenDataUIntString>("PART");

                if (parts[i].IntegerValue != i)
                {
                    throw new InvalidDataException($"Part index for: {parts[i].StringValue} does not match current index.");
                }

                var boneIndex = animModel.Bones.FindIndex(x => x.BoneName.Equals(parts[i].StringValue, StringComparison.CurrentCultureIgnoreCase));

                if(boneIndex != -1)
                {
                    indexMap[boneIndex] = i + 1;
                }
            }

            var frameRate = (int)reader.RequestNextTokenOfType<TokenDataUInt>("FRAMERATE").Value;
            var frameCount = (int)reader.RequestNextTokenOfType<TokenDataUInt>("NUMFRAMES").Value;

            anim.FrameRate = frameRate;

            var transforms = new List<List<(Vector3, Quaternion)>>(frameCount);

            for (int i = 0; i < frameCount; i++)
            {
                var frame = reader.RequestNextTokenOfType<TokenDataUInt>("FRAME").Value;



                transforms.Add(new(partCount));

                for (int p = 0; p < partCount; p++)
                {
                    var part = (int)reader.RequestNextTokenOfType<TokenDataUInt>("PART").Value;
                    var offset = reader.RequestNextTokenOfType<TokenDataVector3>("OFFSET").Value;
                    var xRow = reader.RequestNextTokenOfType<TokenDataVector3>("X").Value;
                    var yRow = reader.RequestNextTokenOfType<TokenDataVector3>("Y").Value;
                    var zRow = reader.RequestNextTokenOfType<TokenDataVector3>("Z").Value;

                    var matrix = new Matrix4x4(
                        xRow.X, xRow.Y, xRow.Z, 0,
                        yRow.X, yRow.Y, yRow.Z, 0,
                        zRow.X, zRow.Y, zRow.Z, 0,
                        0, 0, 0, 1
                        );

                    transforms[i].Add((offset, Quaternion.CreateFromRotationMatrix(matrix)));
                }
            }

            return anim;
        }
    }
}
