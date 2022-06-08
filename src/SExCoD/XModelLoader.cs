using System.Numerics;
using CallOfFile;
using SELib;

namespace SExCoD
{
    /// <summary>
    /// A class that exposes methods for reading XMODEL files.
    /// </summary>
    internal class XModelLoader
    {
        /// <summary>
        /// Vertex Token Names
        /// </summary>
        internal static string[] VertexCountNames = new[] { "NUMVERTS", "NUMVERTS32" };

        /// <summary>
        /// Vertex Token Names
        /// </summary>
        internal static string[] VertexNames = new[] { "VERT", "VERT32" };

        /// <summary>
        /// Tri Token Names
        /// </summary>
        internal static string[] TriNames = new[] { "TRI", "TRI16" };

        /// <summary>
        /// Handles reading a <see cref="SEModel"/> from an XMODEL.
        /// </summary>
        /// <param name="filePath">The file to read from.</param>
        /// <param name="skeletonOnly">Whether or not to only read the skeleton.</param>
        /// <returns>A <see cref="SEModel"/> consumed from an XMODEL.</returns>
        public static SEModel Read(string filePath, bool skeletonOnly = false)
        {
            using var reader = TokenReader.CreateReader(filePath);
            return Read(reader, skeletonOnly);
        }

        /// <summary>
        /// Handles reading a <see cref="SEModel"/> from an XMODEL.
        /// </summary>
        /// <param name="reader">The reader to read from.</param>
        /// <param name="skeletonOnly">Whether or not to only read the skeleton.</param>
        /// <returns>A <see cref="SEModel"/> consumed from an XMODEL.</returns>
        public static SEModel Read(TokenReader reader, bool skeletonOnly = false)
        {
            // First check that we have our version and model identifiers.
            // We don't bother checking version, if the data is good, we good.
            reader.RequestNextTokenOfType<TokenData>("MODEL");
            reader.RequestNextTokenOfType<TokenDataUInt>("VERSION");

            var model = new SEModel();

            var boneCount = (int)reader.RequestNextTokenOfType<TokenDataUInt>("NUMBONES").Value;

            // We'll keep a list then add all at once as we need
            // to first get the bone infos then get the data
            // while also calculating local data as CoD XModel
            // stores world data.
            var boneInfos       = new TokenDataBoneInfo[boneCount];
            var boneLocalLocs   = new Vector3[boneCount];
            var boneWorldLocs   = new Vector3[boneCount];
            var boneLocalQuats  = new Quaternion[boneCount];
            var boneWorldQuats  = new Quaternion[boneCount];
            var boneScales      = new Vector3[boneCount];

            // We need to check for cosmetic bones.
            var nextToken = reader.RequestNextToken();
            if(nextToken.Token.Name == "NUMCOSMETICBONES")
                nextToken = reader.RequestNextToken();

            for (int i = 0; i < boneCount; i++)
            {
                boneInfos[i] = nextToken.Cast<TokenDataBoneInfo>("BONE");

                if (boneInfos[i].BoneIndex != i)
                {
                    throw new InvalidDataException($"Bone index for: {boneInfos[i]} does not match current index.");
                }

                nextToken = reader.RequestNextToken();
            }

            for (int i = 0; i < boneCount; i++)
            {
                var boneIndex = nextToken.Cast<TokenDataUInt>("BONE").Value;
                var offset = reader.RequestNextTokenOfType<TokenDataVector3>("OFFSET").Value * 2.54f;
                var scale = Vector3.One;
                var p = boneInfos[i].BoneParentIndex;

                nextToken = reader.RequestNextToken();

                if (nextToken.Token.Name == "SCALE")
                {
                    scale = ((TokenDataVector3)nextToken).Value;
                    nextToken = reader.RequestNextToken();
                }

                var xRow = nextToken.Cast<TokenDataVector3>("X").Value;
                var yRow = reader.RequestNextTokenOfType<TokenDataVector3>("Y").Value;
                var zRow = reader.RequestNextTokenOfType<TokenDataVector3>("Z").Value;
                var matrix = new Matrix4x4(
                    xRow.X, xRow.Y, xRow.Z, 0,
                    yRow.X, yRow.Y, yRow.Z, 0,
                    zRow.X, zRow.Y, zRow.Z, 0,
                    0, 0, 0, 1
                    );
                var asQuat = Quaternion.CreateFromRotationMatrix(matrix);

                // Add world info
                boneWorldLocs[i] = offset;
                boneWorldQuats[i] = asQuat;
                boneScales[i] = scale;

                // If we have a parent, we'll need to use it to calculate local data.
                if (p != -1)
                {
                    boneLocalQuats[i] = Quaternion.Conjugate(boneWorldQuats[p]) * boneWorldQuats[i];
                    boneLocalLocs[i] = Vector3.Transform(boneWorldLocs[i] - boneWorldLocs[p], Quaternion.Conjugate(boneWorldQuats[p]));
                }
                else
                {
                    boneLocalLocs[i] = boneWorldLocs[i];
                    boneLocalQuats[i] = boneWorldQuats[i];
                }

                nextToken = reader.RequestNextToken();
            }

            // Now we can build our SEModel list
            // TODO: Moved SELib to System.Numerics
            // so this can be done above without the need
            // for temporary buffers.
            for (int i = 0; i < boneCount; i++)
            {
                model.AddBone(
                    boneInfos[i].Name.ToLower(),
                    boneInfos[i].BoneParentIndex,
                    new(boneWorldLocs[i].X,
                        boneWorldLocs[i].Y,
                        boneWorldLocs[i].Z),
                    new(boneWorldQuats[i].X,
                        boneWorldQuats[i].Y,
                        boneWorldQuats[i].Z,
                        boneWorldQuats[i].W),
                    new(boneLocalLocs[i].X,
                        boneLocalLocs[i].Y,
                        boneLocalLocs[i].Z),
                    new(boneLocalQuats[i].X,
                        boneLocalQuats[i].Y,
                        boneLocalQuats[i].Z,
                        boneLocalQuats[i].W),
                    new(boneScales[i].X,
                        boneScales[i].Y,
                        boneScales[i].Z));
            }

            if (skeletonOnly)
                return model;

            var vertCount = (int)nextToken.Cast<TokenDataUInt>(VertexCountNames).Value;
            var vertices = new List<TokenDataVector3>(vertCount);
            var weightCounts = new List<(int, int)>(vertCount);
            var weights = new List<TokenDataBoneWeight>(vertCount * 16);
            var instances = new List<List<(SEModelMesh, int)>>(vertCount);

            for (int i = 0; i < vertCount; i++)
            {
                reader.RequestNextTokenOfType<TokenDataUInt>(VertexNames);
                vertices.Add(reader.RequestNextTokenOfType<TokenDataVector3>("OFFSET"));
                instances.Add(new());

                var weightCount = reader.RequestNextTokenOfType<TokenDataUInt>("BONES");

                weightCounts.Add((weights.Count, (int)weightCount.Value));

                for (int j = 0; j < weightCount.Value; j++)
                {
                    var weight = reader.RequestNextTokenOfType<TokenDataBoneWeight>("BONE");

                    if (weight.BoneIndex < 0 || weight.BoneIndex >= boneCount)
                        throw new IndexOutOfRangeException($"Bone index for vertice: {i} is out of range.");
                    if (weight.BoneWeight < 0)
                        throw new IndexOutOfRangeException($"Bone weight for vertice: {i} is negative.");

                    weights.Add(weight);
                }
            }

            var faceCount = (int)reader.RequestNextTokenOfType<TokenDataUInt>("NUMFACES").Value;

            var tris          = new List<TokenDataTri>(faceCount);
            var vertexIndices = new List<TokenDataUInt>(faceCount * 3);
            var normals       = new List<TokenDataVector3>(faceCount * 3);
            var colors        = new List<TokenDataVector4>(faceCount * 3);
            var uvLayers      = new List<TokenDataUVSet>(faceCount * 3);

            for (int i = 0; i < faceCount; i++)
            {
                tris.Add(reader.RequestNextTokenOfType<TokenDataTri>(TriNames));

                for (int v = 0; v < 3; v++)
                {
                    vertexIndices.Add(reader.RequestNextTokenOfType<TokenDataUInt>(VertexNames));
                    normals.Add(reader.RequestNextTokenOfType<TokenDataVector3>("NORMAL"));
                    colors.Add(reader.RequestNextTokenOfType<TokenDataVector4>("COLOR"));
                    uvLayers.Add(reader.RequestNextTokenOfType<TokenDataUVSet>("UV"));
                }
            }

            var objCount = (int)reader.RequestNextTokenOfType<TokenDataUInt>("NUMOBJECTS").Value;

            for (int i = 0; i < objCount; i++)
            {
                // We don't care for this marvooning
                reader.RequestNextToken();
            }

            var matCount = (int)reader.RequestNextTokenOfType<TokenDataUInt>("NUMMATERIALS").Value;
            var materials = new List<TokenDataUIntStringX3>(matCount);

            for (int i = 0; i < matCount; i++)
            {
                var material = reader.RequestNextTokenOfType<TokenDataUIntStringX3>("MATERIAL");

                if (material.IntegerValue != i)
                {
                    throw new InvalidDataException($"Material index for: {material.StringValue1} does not match current index.");
                }

                materials.Add(material);

                // Skip unused material params
                for (int s = 0; s < 12; s++)
                    reader.RequestNextToken();
            }

            // For each material, create a mesh, and a material entry.
            foreach (var material in materials)
            {
                var newMesh = new SEModelMesh();
                newMesh.MaterialReferenceIndicies.Add(model.Materials.Count);
                model.AddMesh(newMesh);
                model.AddMaterial(new SEModelMaterial()
                {
                    Name = material.StringValue1,
                    MaterialData = new SEModelSimpleMaterial(),
                });
            }

            // A local list of indices we have built for this face.
            Span<int> indices = stackalloc int[3];

            // Now we'll actually build the geometry.
            for (int i = 0, v = 0; i < faceCount; i++)
            {
                var tri = tris[i];
                var mesh = model.Meshes[tri.MaterialIndex];

                for (int j = 0; j < 3; j++, v++)
                {
                    var vertIndex = (int)vertexIndices[v].Value;

                    var position  = vertices[vertIndex].Value * 2.54f;
                    var normal    = normals[v].Value;
                    var color     = colors[v].Value;
                    var uv        = uvLayers[v].UVs[0];
                    var existing  = instances[vertIndex];
                    var faceIndex = -1;

                    // Update UV
                    uv = new(uv.X, 1.0f - uv.Y);

                    // We need to check against other instances of faces
                    // using this vertex index, if we find an existing 
                    // instance of that vertex otherwise we end up with
                    // hundres of thousands of verts per face vertex
                    foreach (var c in existing)
                    {
                        if (c.Item1 == mesh)
                        {
                            if (mesh.Verticies[c.Item2].VertexNormal == normal && mesh.Verticies[c.Item2].UVSets[0] == uv)
                            {
                                faceIndex = c.Item2;
                                break;
                            }
                        }
                    }

                    // We failed to find a vertex, we're adding a new one.
                    if (faceIndex == -1)
                    {
                        faceIndex = mesh.Verticies.Count;

                        var newVertex = new SEModelVertex()
                        {
                            Position = position,
                            VertexNormal = normal,
                            VertexColor = new(
                                (byte)(color.X * 255),
                                (byte)(color.Y * 255),
                                (byte)(color.Z * 255),
                                (byte)(color.W * 255)),
                        };
                        newVertex.UVSets.Add(uv);

                        var (weightsIdx, weightCount) = weightCounts[vertIndex];

                        // We'll need to keep a sum then normalize or else we can have
                        // some nasty effects in tools like Maya if our weights are even
                        // slightly above or below 1.0 in total
                        var weightSum = 0.0f;

                        for (int w = 0; w < weightCount; w++)
                        {
                            var weight = weights[weightsIdx + w];
                            newVertex.Weights.Add(new()
                            {
                                BoneIndex = (uint)weight.BoneIndex,
                                BoneWeight = weight.BoneWeight,
                            });

                            weightSum += weight.BoneWeight;
                        }

                        //// Ensure we're normalized
                        foreach (var weight in newVertex.Weights)
                            weight.BoneWeight /= weightSum;

                        mesh.AddVertex(newVertex);
                        existing.Add((mesh, faceIndex));
                    }

                    indices[j] = faceIndex;
                }

                // Match our format, CoD is reversed.
                mesh.Faces.Add(new((uint)indices[0], (uint)indices[1], (uint)indices[2]));
            }

            return model;
        }

    }
}
