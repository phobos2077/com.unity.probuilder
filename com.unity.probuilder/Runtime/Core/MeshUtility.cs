﻿using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections;

namespace UnityEngine.ProBuilder
{
	/// <summary>
	/// Functions for generating mesh attributes and various other mesh utilities.
	/// </summary>
	public static class MeshUtility
	{
		/// <summary>
		/// Create an array of @"UnityEngine.ProBuilder.Vertex" values that are ordered as individual triangles. This modifies the source mesh to match the new individual triangles format.
		/// </summary>
		/// <param name="mesh">The mesh to extract vertexes from, and apply per-triangle topology to.</param>
		/// <returns>A @"UnityEngine.ProBuilder.Vertex" array of the per-triangle vertexes.</returns>
		internal static Vertex[] GeneratePerTriangleMesh(Mesh mesh)
		{
            if (mesh == null)
                throw new ArgumentNullException("mesh");

            Vertex[] vertexes = mesh.GetVertexes();
            int smc = mesh.subMeshCount;
            Vertex[] tv = new Vertex[mesh.triangles.Length];
            int[][] triangles = new int[smc][];
            int triIndex = 0;

            for (int s = 0; s < smc; s++)
			{
				triangles[s] = mesh.GetTriangles(s);
				int tl = triangles[s].Length;

				for(int i = 0; i < tl; i++)
				{
					tv[triIndex++] = new Vertex( vertexes[triangles[s][i]] );
					triangles[s][i] = triIndex - 1;
				}
			}

			Vertex.SetMesh(mesh, tv);

			mesh.subMeshCount = smc;

			for(int s = 0; s < smc; s++)
				mesh.SetTriangles(triangles[s], s);

			return tv;
		}

		/// <summary>
		/// Generate tangents and apply them.
		/// </summary>
		/// <param name="mesh">The UnityEngine.Mesh mesh target.</param>
		public static void GenerateTangent(Mesh mesh)
		{
            if (mesh == null)
                throw new System.ArgumentNullException("mesh");

            // http://answers.unity3d.com/questions/7789/calculating-tangents-vector4.html

            // speed up math by copying the mesh arrays
            int[] triangles = mesh.triangles;
            Vector3[] vertexes = mesh.vertices;
            Vector2[] uv = mesh.uv;
            Vector3[] normals = mesh.normals;

            //variable definitions
            int triangleCount = triangles.Length;
            int vertexCount = vertexes.Length;

            Vector3[] tan1 = new Vector3[vertexCount];
            Vector3[] tan2 = new Vector3[vertexCount];

            Vector4[] tangents = new Vector4[vertexCount];

			for (long a = 0; a < triangleCount; a += 3)
			{
				long i1 = triangles[a + 0];
				long i2 = triangles[a + 1];
				long i3 = triangles[a + 2];

				Vector3 v1 = vertexes[i1];
				Vector3 v2 = vertexes[i2];
				Vector3 v3 = vertexes[i3];

				Vector2 w1 = uv[i1];
				Vector2 w2 = uv[i2];
				Vector2 w3 = uv[i3];

				float x1 = v2.x - v1.x;
				float x2 = v3.x - v1.x;
				float y1 = v2.y - v1.y;
				float y2 = v3.y - v1.y;
				float z1 = v2.z - v1.z;
				float z2 = v3.z - v1.z;

				float s1 = w2.x - w1.x;
				float s2 = w3.x - w1.x;
				float t1 = w2.y - w1.y;
				float t2 = w3.y - w1.y;

				float r = 1.0f / (s1 * t2 - s2 * t1);

				Vector3 sdir = new Vector3((t2 * x1 - t1 * x2) * r, (t2 * y1 - t1 * y2) * r, (t2 * z1 - t1 * z2) * r);
				Vector3 tdir = new Vector3((s1 * x2 - s2 * x1) * r, (s1 * y2 - s2 * y1) * r, (s1 * z2 - s2 * z1) * r);

				tan1[i1] += sdir;
				tan1[i2] += sdir;
				tan1[i3] += sdir;

				tan2[i1] += tdir;
				tan2[i2] += tdir;
				tan2[i3] += tdir;
			}


			for (long a = 0; a < vertexCount; ++a)
			{
				Vector3 n = normals[a];
				Vector3 t = tan1[a];

				Vector3.OrthoNormalize(ref n, ref t);
				tangents[a].x = t.x;
				tangents[a].y = t.y;
				tangents[a].z = t.z;

				tangents[a].w = (Vector3.Dot(Vector3.Cross(n, t), tan2[a]) < 0.0f) ? -1.0f : 1.0f;
			}

			mesh.tangents = tangents;
		}

		/// <summary>
		/// Performs a deep copy of a mesh and returns a new mesh object.
		/// </summary>
		/// <param name="source">The source mesh.</param>
		/// <returns>A new UnityEngine.Mesh object with the same values as source.</returns>
		public static Mesh DeepCopy(Mesh source)
		{
			Mesh m = new Mesh();
			CopyTo(source, m);
			return m;
		}

		/// <summary>
		/// Copy source mesh values to destination mesh.
		/// </summary>
		/// <param name="source">The mesh from which to copy attributes.</param>
		/// <param name="destination">The destination mesh to copy attribute values to.</param>
		/// <exception cref="ArgumentNullException">Throws if source or destination is null.</exception>
		public static void CopyTo(Mesh source, Mesh destination)
		{
            if (source == null)
                throw new System.ArgumentNullException("source");

            if (destination == null)
                throw new System.ArgumentNullException("destination");

            Vector3[] v = new Vector3[source.vertices.Length];
            int[][] t = new int[source.subMeshCount][];
            Vector2[] u = new Vector2[source.uv.Length];
            Vector2[] u2 = new Vector2[source.uv2.Length];
            Vector4[] tan = new Vector4[source.tangents.Length];
            Vector3[] n = new Vector3[source.normals.Length];
            Color32[] c = new Color32[source.colors32.Length];

            System.Array.Copy(source.vertices, v, v.Length);

			for(int i = 0; i < t.Length; i++)
				t[i] = source.GetTriangles(i);

			System.Array.Copy(source.uv, u, u.Length);
			System.Array.Copy(source.uv2, u2, u2.Length);
			System.Array.Copy(source.normals, n, n.Length);
			System.Array.Copy(source.tangents, tan, tan.Length);
			System.Array.Copy(source.colors32, c, c.Length);

			destination.Clear();
			destination.name = source.name;

			destination.vertices = v;

			destination.subMeshCount = t.Length;

			for(int i = 0; i < t.Length; i++)
				destination.SetTriangles(t[i], i);

			destination.uv = u;
			destination.uv2 = u2;
			destination.tangents = tan;
			destination.normals = n;
			destination.colors32 = c;
		}

        /// <summary>
        /// Get a mesh attribute from either the MeshFilter.sharedMesh or the MeshRenderer.additionalVertexStreams mesh. The additional vertex stream mesh has priority.
        /// </summary>
        /// <typeparam name="T">The type of the attribute to fetch.</typeparam>
        /// <param name="gameObject">The GameObject with the MeshFilter and (optional) MeshRenderer to search for mesh attributes.</param>
        /// <param name="attributeGetter">The function used to extract mesh attribute.</param>
        /// <returns>A List of the mesh attribute values from the Additional Vertex Streams mesh if it exists and contains the attribute, or the MeshFilter.sharedMesh attribute values.</returns>
        internal static T GetMeshChannel<T>(GameObject gameObject, Func<Mesh, T> attributeGetter) where T : IList
		{
            if (gameObject == null)
                throw new System.ArgumentNullException("gameObject");

            if (attributeGetter == null)
                throw new System.ArgumentNullException("attributeGetter");

			MeshFilter mf = gameObject.GetComponent<MeshFilter>();
			Mesh mesh = mf != null ? mf.sharedMesh : null;
			T res = default(T);

			if(mesh == null)
				return res;

			int vertexCount = mesh.vertexCount;

#if !UNITY_4_6 && !UNITY_4_7
			MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
			Mesh vertexStream = renderer != null ? renderer.additionalVertexStreams : null;

			if(vertexStream != null)
			{
				res = attributeGetter(vertexStream);

				if(res != null && res.Count == vertexCount)
					return res;
			}
#endif
			res = attributeGetter(mesh);

			return res != null && res.Count == vertexCount ? res : default(T);
		}

        /// <summary>
        /// Print a detailed string summary of the mesh attributes.
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
		public static string Print(Mesh mesh)
		{
            if (mesh == null)
                throw new ArgumentNullException("mesh");

			System.Text.StringBuilder sb = new System.Text.StringBuilder();

			sb.AppendLine(string.Format("vertexes: {0}\ntriangles: {1}\nsubmeshes: {2}", mesh.vertexCount, mesh.triangles.Length, mesh.subMeshCount));

			sb.AppendLine(string.Format("     {0,-28}{1,-28}{2,-28}{3,-28}{4,-28}{5,-28}{6,-28}{7,-28}",
				"Positions",
				"Normals",
				"Colors",
				"Tangents",
				"UV0",
				"UV2",
				"UV3",
				"UV4"));

			Vector3[] positions = mesh.vertices;
			Vector3[] normals = mesh.normals;
			Color[] colors = mesh.colors;
			Vector4[] tangents = mesh.tangents;

			List<Vector4> uv0 = new List<Vector4>();
			Vector2[] uv2 = mesh.uv2;
			List<Vector4> uv3 = new List<Vector4>();
			List<Vector4> uv4 = new List<Vector4>();

			mesh.GetUVs(0, uv0);
			mesh.GetUVs(2, uv3);
			mesh.GetUVs(3, uv4);

			if( positions != null && positions.Count() != mesh.vertexCount)
				positions = null;
			if( colors != null && colors.Count() != mesh.vertexCount)
				colors = null;
			if( tangents != null && tangents.Count() != mesh.vertexCount)
				tangents = null;
			if( uv0.Count() != mesh.vertexCount)
				uv0 = null;
			if( uv2.Count() != mesh.vertexCount)
				uv2 = null;
			if( uv3.Count() != mesh.vertexCount)
				uv3 = null;
			if( uv4.Count() != mesh.vertexCount)
				uv4 = null;

			for(int i = 0, c = mesh.vertexCount; i < c; i ++)
			{
				sb.AppendLine(string.Format("{8,-5}{0,-28}{1,-28}{2,-28}{3,-28}{4,-28}{5,-28}{6,-28}{7,-28}",
					positions == null 	? "null" : string.Format("{0:F3}, {1:F3}, {2:F3}", positions[i].x, positions[i].y, positions[i].z),
					normals == null 	? "null" : string.Format("{0:F3}, {1:F3}, {2:F3}", normals[i].x, normals[i].y, normals[i].z),
					colors == null 		? "null" : string.Format("{0:F2}, {1:F2}, {2:F2}, {3:F2}", colors[i].r, colors[i].g, colors[i].b, colors[i].a),
					tangents == null 	? "null" : string.Format("{0:F2}, {1:F2}, {2:F2}, {3:F2}", tangents[i].x, tangents[i].y, tangents[i].z, tangents[i].w),
					uv0 == null 		? "null" : string.Format("{0:F2}, {1:F2}, {2:F2}, {3:F2}", uv0[i].x, uv0[i].y, uv0[i].z, uv0[i].w),
					uv2 == null 		? "null" : string.Format("{0:F2}, {1:F2}", uv2[i].x, uv2[i].y),
					uv3 == null 		? "null" : string.Format("{0:F2}, {1:F2}, {2:F2}, {3:F2}", uv3[i].x, uv3[i].y, uv3[i].z, uv3[i].w),
					uv4 == null 		? "null" : string.Format("{0:F2}, {1:F2}, {2:F2}, {3:F2}", uv4[i].x, uv4[i].y, uv4[i].z, uv4[i].w),
					i));
			}

			for(int i = 0; i < mesh.triangles.Length; i+=3)
				sb.AppendLine(string.Format("{0}, {1}, {2}", mesh.triangles[i], mesh.triangles[i+1], mesh.triangles[i+2]));

			return sb.ToString();
		}

		/// <summary>
		/// Get the number of indexes this mesh contains.
		/// </summary>
		/// <param name="mesh">The source mesh to sum submesh index counts from.</param>
		/// <returns>The count of all indexes contained within this meshes submeshes.</returns>
		public static uint GetIndexCount(Mesh mesh)
		{
			uint sum = 0;

			if (mesh == null)
				return sum;

			for (int i = 0, c = mesh.subMeshCount; i < c; i++)
				sum += mesh.GetIndexCount(i);

			return sum;
		}

		/// <summary>
		/// Get the number of triangles or quads this mesh contains. Other mesh topologies are not considered.
		/// </summary>
		/// <param name="mesh">The source mesh to sum submesh primitive counts from.</param>
		/// <returns>The count of all triangles or quads contained within this meshes submeshes.</returns>
		public static uint GetPrimitiveCount(Mesh mesh)
		{
			uint sum = 0;

			if (mesh == null)
				return sum;

			for (int i = 0, c = mesh.subMeshCount; i < c; i++)
			{
				if(mesh.GetTopology(i) == MeshTopology.Triangles)
					sum += mesh.GetIndexCount(i) / 3;
				else if(mesh.GetTopology(i) == MeshTopology.Quads)
					sum += mesh.GetIndexCount(i) / 4;
			}

			return sum;
		}

		/// <summary>
        /// Compile a UnityEngine.Mesh from a ProBuilderMesh.
        /// </summary>
        /// <param name="probuilderMesh">The mesh source.</param>
        /// <param name="targetMesh">Destination UnityEngine.Mesh.</param>
        /// <param name="preferredTopology">If specified, the function will try to create topology matching the reqested format (and falling back on triangles where necessary).</param>
        /// <returns>The resulting material array from the compiled faces array. This is suitable to assign to the MeshRenderer.sharedMaterials property.</returns>
        public static Material[] Compile(ProBuilderMesh probuilderMesh, Mesh targetMesh, MeshTopology preferredTopology = MeshTopology.Triangles)
        {
            if (probuilderMesh == null)
                throw new ArgumentNullException("probuilderMesh");

            if (targetMesh == null)
                throw new ArgumentNullException("targetMesh");

            targetMesh.Clear();

            targetMesh.vertices = probuilderMesh.positionsInternal;
            targetMesh.uv = probuilderMesh.texturesInternal;

            if (probuilderMesh.HasArrays(MeshArrays.Texture2))
            {
                List<Vector4> uvChannel = new List<Vector4>();
                probuilderMesh.GetUVs(2, uvChannel);
                targetMesh.SetUVs(2, uvChannel);
            }

            if (probuilderMesh.HasArrays(MeshArrays.Texture3))
            {
                List<Vector4> uvChannel = new List<Vector4>();
                probuilderMesh.GetUVs(3, uvChannel);
                targetMesh.SetUVs(3, uvChannel);
            }

            targetMesh.normals = probuilderMesh.CalculateNormals();

            MeshUtility.GenerateTangent(targetMesh);

            if (probuilderMesh.HasArrays(MeshArrays.Color))
                targetMesh.colors = probuilderMesh.colorsInternal;

            var submeshes = Submesh.GetSubmeshes(probuilderMesh.facesInternal, preferredTopology);
            targetMesh.subMeshCount = submeshes.Length;

            for (int i = 0; i < targetMesh.subMeshCount; i++)
                targetMesh.SetIndices(submeshes[i].m_Indexes, submeshes[i].m_Topology, i, false);

            targetMesh.name = string.Format("pb_Mesh{0}", probuilderMesh.id);

            return submeshes.Select(x => x.m_Material).ToArray();
        }

        /// <summary>
        /// Create a new UV channel and return it using each @"UnityEngine.ProBuilder.Face" @"UnityEngine.ProBuilder.AutoUnwrapSettings" property.
        /// </summary>
        /// <param name="mesh">The target mesh.</param>
        /// <returns>A new array of texture coordinates.</returns>
        internal static Vector2[] GetUVs(ProBuilderMesh mesh)
		{
			int n = -2;
			Dictionary<int, List<Face>> textureGroups = new Dictionary<int, List<Face>>();
			bool anyWorldSpace = false;
			List<Face> group;

			foreach (Face f in mesh.facesInternal)
			{
				if (f.uv.useWorldSpace)
					anyWorldSpace = true;

				if (f == null || f.manualUV)
					continue;

				if (f.textureGroup > 0 && textureGroups.TryGetValue(f.textureGroup, out group))
					group.Add(f);
				else
					textureGroups.Add(f.textureGroup > 0 ? f.textureGroup : n--, new List<Face>() { f });
			}

			n = 0;

			Vector3[] world = anyWorldSpace ? mesh.VertexesInWorldSpace() : null;
			Vector2[] uvs = mesh.texturesInternal != null && mesh.texturesInternal.Length == mesh.vertexCount ? mesh.texturesInternal : new Vector2[mesh.vertexCount];

			foreach (KeyValuePair<int, List<Face>> kvp in textureGroups)
			{
				Vector3 nrm;
				int[] indexes = kvp.Value.SelectMany(x => x.distinctIndexesInternal).ToArray();

				if (kvp.Value.Count > 1)
					nrm = Projection.FindBestPlane(mesh.positionsInternal, indexes).normal;
				else
					nrm = Math.Normal(mesh, kvp.Value[0]);

				if (kvp.Value[0].uv.useWorldSpace)
					UnwrappingUtility.PlanarMap2(world, uvs, indexes, kvp.Value[0].uv, mesh.transform.TransformDirection(nrm));
				else
					UnwrappingUtility.PlanarMap2(mesh.positionsInternal, uvs, indexes, kvp.Value[0].uv, nrm);
			}

			return uvs;
		}

		/// <summary>
		/// Creates a new array of vertexes with values from a UnityEngine.Mesh.
		/// </summary>
		/// <param name="mesh">The source mesh.</param>
		/// <returns>An array of vertexes.</returns>
		public static Vertex[] GetVertexes(this Mesh mesh)
		{
			if (mesh == null)
				return null;

			int vertexCount = mesh.vertexCount;
			Vertex[] v = new Vertex[vertexCount];

			Vector3[] positions = mesh.vertices;
			Color[] colors = mesh.colors;
			Vector3[] normals = mesh.normals;
			Vector4[] tangents = mesh.tangents;
			Vector2[] uv0s = mesh.uv;
			Vector2[] uv2s = mesh.uv2;
			List<Vector4> uv3s = new List<Vector4>();
			List<Vector4> uv4s = new List<Vector4>();
			mesh.GetUVs(2, uv3s);
			mesh.GetUVs(3, uv4s);

			bool _hasPositions = positions != null && positions.Count() == vertexCount;
			bool _hasColors = colors != null && colors.Count() == vertexCount;
			bool _hasNormals = normals != null && normals.Count() == vertexCount;
			bool _hasTangents = tangents != null && tangents.Count() == vertexCount;
			bool _hasUv0 = uv0s != null && uv0s.Count() == vertexCount;
			bool _hasUv2 = uv2s != null && uv2s.Count() == vertexCount;
			bool _hasUv3 = uv3s.Count() == vertexCount;
			bool _hasUv4 = uv4s.Count() == vertexCount;

			for (int i = 0; i < vertexCount; i++)
			{
				v[i] = new Vertex();

				if (_hasPositions)
					v[i].position = positions[i];

				if (_hasColors)
					v[i].color = colors[i];

				if (_hasNormals)
					v[i].normal = normals[i];

				if (_hasTangents)
					v[i].tangent = tangents[i];

				if (_hasUv0)
					v[i].uv0 = uv0s[i];

				if (_hasUv2)
					v[i].uv2 = uv2s[i];

				if (_hasUv3)
					v[i].uv3 = uv3s[i];

				if (_hasUv4)
					v[i].uv4 = uv4s[i];
			}

			return v;
		}

		/// <summary>
		/// Merge coincident vertexes where possible, optimizing the vertex count of a UnityEngine.Mesh.
		/// </summary>
		/// <param name="mesh">The mesh to optimize.</param>
		/// <param name="vertexes">
		/// If provided these values are used in place of extracting attributes from the Mesh.
		/// <br />
		/// This is a performance optimization for when this array already exists. If not provided this array will be
		/// automatically generated for you.
		/// </param>
		public static void CollapseSharedVertexes(Mesh mesh, Vertex[] vertexes = null)
		{
            if (mesh == null)
                throw new System.ArgumentNullException("mesh");

			if (vertexes == null)
				vertexes = mesh.GetVertexes();

			int smc = mesh.subMeshCount;
			List<Dictionary<Vertex, int>> subVertexes = new List<Dictionary<Vertex, int>>();
			int[][] tris = new int[smc][];
			int subIndex = 0;

			for (int i = 0; i < smc; ++i)
			{
				tris[i] = mesh.GetTriangles(i);
				Dictionary<Vertex, int> newVertexes = new Dictionary<Vertex, int>();

				for (int n = 0; n < tris[i].Length; n++)
				{
					Vertex v = vertexes[tris[i][n]];
					int index;

					if (newVertexes.TryGetValue(v, out index))
					{
						tris[i][n] = index;
					}
					else
					{
						tris[i][n] = subIndex;
						newVertexes.Add(v, subIndex);
						subIndex++;
					}
				}

				subVertexes.Add(newVertexes);
			}

			Vertex[] collapsed = subVertexes.SelectMany(x => x.Keys).ToArray();
			Vertex.SetMesh(mesh, collapsed);
			mesh.subMeshCount = smc;
			for (int i = 0; i < smc; i++)
				mesh.SetTriangles(tris[i], i);
		}
	}
}