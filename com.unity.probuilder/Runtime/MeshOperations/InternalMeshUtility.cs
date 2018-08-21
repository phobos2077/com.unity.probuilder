using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.ProBuilder;

namespace UnityEngine.ProBuilder.MeshOperations
{
	static class InternalMeshUtility
	{
		/// <summary>
		/// Averages shared normals with the mask of all (indexes contained in perimeter edge)
		/// </summary>
		internal static Vector3 AverageNormalWithIndexes(SharedVertex shared, int[] all, Vector3[] norm )
		{
			Vector3 n = Vector3.zero;
			int count = 0;
			for(int i = 0; i < all.Length; i++)
			{
				// this is a point in the perimeter, add it to the average
				if( shared.Contains(all[i]) )
				{
					n += norm[all[i]];
					count++;
				}
			}
			return (n / (float)count);
		}

		static ProBuilderMesh CreateMeshFromSplit(List<Vertex> vertexes,
			List<Face> faces,
			Dictionary<int, int> sharedVertexLookup,
			Dictionary<int, int> sharedTextureLookup,
			Dictionary<int, int> remap)
		{
			// finalize mesh
			var sv = new Dictionary<int, int>();
			var st = new Dictionary<int, int>();

			foreach (var f in faces)
			{
				for (int i = 0, c = f.indexesInternal.Length; i < c; i++)
					f.indexesInternal[i] = remap[f.indexesInternal[i]];

				f.InvalidateCache();
			}

			foreach (var kvp in remap)
			{
				int v;

				if(sharedVertexLookup.TryGetValue(kvp.Key, out v))
					sv.Add(kvp.Value, v);

				if(sharedTextureLookup.TryGetValue(kvp.Key, out v))
					st.Add(kvp.Value, v);
			}

			return ProBuilderMesh.Create(vertexes, faces, SharedVertex.ToSharedVertexes(sv), st.Any() ? SharedVertex.ToSharedVertexes(st) : null);
		}

		/// <summary>
		/// Break a ProBuilder mesh into multiple meshes if it's vertex count is greater than maxVertexCount.
		/// </summary>
		/// <returns></returns>
		internal static List<ProBuilderMesh> SplitByMaxVertexCount(IList<Vertex> vertexes, IList<Face> faces, IList<SharedVertex> sharedVertexes, IList<SharedVertex> sharedTextures, uint maxVertexCount = ProBuilderMesh.maxVertexCount)
		{
			uint vertexCount = (uint) vertexes.Count;
			uint meshCount = System.Math.Max(1u, vertexCount / maxVertexCount);

			if(meshCount < 2)
				return new List<ProBuilderMesh>() { ProBuilderMesh.Create(vertexes, faces, sharedVertexes, sharedTextures) };

			var sharedVertexLookup = new Dictionary<int, int>();
			SharedVertex.GetSharedVertexLookup(sharedVertexes, sharedVertexLookup);

			var sharedTextureLookup = new Dictionary<int, int>();
			SharedVertex.GetSharedVertexLookup(sharedTextures, sharedTextureLookup);

			var meshes = new List<ProBuilderMesh>();
			var mv = new List<Vertex>();
			var mf = new List<Face>();
			var remap = new Dictionary<int, int>();

			foreach (var face in faces)
			{
				if (mv.Count + face.distinctIndexes.Count > maxVertexCount)
				{
					// finalize mesh
					meshes.Add( CreateMeshFromSplit(mv, mf, sharedVertexLookup, sharedTextureLookup, remap) );
					mv.Clear();
					mf.Clear();
					remap.Clear();
				}

				foreach (int i in face.distinctIndexes)
				{
					mv.Add(vertexes[i]);
					remap.Add(i, mv.Count - 1);
				}

				mf.Add(face);
			}

			if (mv.Any())
				meshes.Add( CreateMeshFromSplit(mv, mf, sharedVertexLookup, sharedTextureLookup, remap) );

			return meshes;
		}

		/// <summary>
		/// Given an array of "donors", this method returns a merged ProBuilderMesh.
		/// </summary>
		/// <param name="meshes"></param>
		/// <returns></returns>
		public static List<ProBuilderMesh> CombineObjects(IEnumerable<ProBuilderMesh> meshes)
		{
			if (meshes == null)
				throw new ArgumentNullException("meshes");

			if (!meshes.Any() || meshes.Count() < 2)
				return null;

			var vertexes = new List<Vertex>();
			var faces = new List<Face>();
			var sharedVertexes = new List<SharedVertex>();
			var sharedTextures = new List<SharedVertex>();
			int offset = 0;

			foreach (var mesh in meshes)
			{
				var meshVertexCount = mesh.vertexCount;
				var transform = mesh.transform;
				var meshVertexes = mesh.GetVertexes();
				var meshFaces = mesh.facesInternal;
				var meshSharedVertexes = mesh.sharedVertexes;
				var meshSharedTextures = mesh.sharedTextures;

				for (int i = 0; i < meshVertexCount; i++)
					vertexes.Add(transform.TransformVertex(meshVertexes[i]));

				foreach (var face in meshFaces)
				{
					var nf = new Face(face);
					nf.ShiftIndexes(offset);
					// prevents uvs from shifting when being converted from local coords to world space
					if (!nf.manualUV && !nf.uv.useWorldSpace)
						nf.manualUV = true;
					faces.Add(nf);
				}

				foreach (var sv in meshSharedVertexes)
				{
					var nsv = new SharedVertex(sv);
					nsv.ShiftIndexes(offset);
					sharedVertexes.Add(nsv);
				}

				foreach (var st in meshSharedTextures)
				{
					var nst = new SharedVertex(st);
					nst.ShiftIndexes(offset);
					sharedTextures.Add(nst);
				}

				offset += meshVertexCount;
			}

			return SplitByMaxVertexCount(vertexes, faces, sharedVertexes, sharedTextures);
		}

		/// <summary>
		/// "ProBuilder-ize" function
		/// </summary>
		/// <param name="t"></param>
		/// <param name="preserveFaces"></param>
		/// <returns></returns>
		public static ProBuilderMesh CreateMeshWithTransform(Transform t, bool preserveFaces)
		{
			Mesh m = t.GetComponent<MeshFilter>().sharedMesh;

			Vector3[] m_vertexes = MeshUtility.GetMeshChannel<Vector3[]>(t.gameObject, x => x.vertices);
			Color[] m_colors = MeshUtility.GetMeshChannel<Color[]>(t.gameObject, x => x.colors);
			Vector2[] m_uvs = MeshUtility.GetMeshChannel<Vector2[]>(t.gameObject, x => x.uv);

			List<Vector3> verts = preserveFaces ? new List<Vector3>(m.vertices) : new List<Vector3>();
			List<Color> cols = preserveFaces ? new List<Color>(m.colors) : new List<Color>();
			List<Vector2> uvs = preserveFaces ? new List<Vector2>(m.uv) : new List<Vector2>();
			List<Face> faces = new List<Face>();

			for (int n = 0; n < m.subMeshCount; n++)
			{
				int[] tris = m.GetTriangles(n);
				for (int i = 0; i < tris.Length; i += 3)
				{
					int index = -1;
					if(preserveFaces)
					{
						for(int j = 0; j < faces.Count; j++)
						{
							if(	faces[j].distinctIndexesInternal.Contains(tris[i+0]) ||
								faces[j].distinctIndexesInternal.Contains(tris[i+1]) ||
								faces[j].distinctIndexesInternal.Contains(tris[i+2]))
							{
								index = j;
								break;
							}
						}
					}

					if(index > -1 && preserveFaces)
					{
						int len = faces[index].indexesInternal.Length;
						int[] arr = new int[len + 3];
						System.Array.Copy(faces[index].indexesInternal, 0, arr, 0, len);
						arr[len+0] = tris[i+0];
						arr[len+1] = tris[i+1];
						arr[len+2] = tris[i+2];
						faces[index].indexesInternal = arr;
					}
					else
					{
						int[] faceTris;

						if(preserveFaces)
						{
							faceTris = new int[3]
							{
								tris[i+0],
								tris[i+1],
								tris[i+2]
							};
						}
						else
						{
							verts.Add(m_vertexes[tris[i+0]]);
							verts.Add(m_vertexes[tris[i+1]]);
							verts.Add(m_vertexes[tris[i+2]]);

							cols.Add(m_colors != null ? m_colors[tris[i+0]] : Color.white);
							cols.Add(m_colors != null ? m_colors[tris[i+1]] : Color.white);
							cols.Add(m_colors != null ? m_colors[tris[i+2]] : Color.white);

							uvs.Add(m_uvs[tris[i+0]]);
							uvs.Add(m_uvs[tris[i+1]]);
							uvs.Add(m_uvs[tris[i+2]]);

							faceTris = new int[3] { i+0, i+1, i+2 };
						}

						faces.Add(
							new Face(
								faceTris,
								t.GetComponent<MeshRenderer>().sharedMaterials[n],
								AutoUnwrapSettings.tile,
								0,		// smoothing group
								-1,		// texture group
								-1,		// element group
								true 	// manualUV
							));
					}
				}
			}

			GameObject go = (GameObject) Object.Instantiate(t.gameObject);
			go.GetComponent<MeshFilter>().sharedMesh = null;

			ProBuilderMesh pb = go.AddComponent<ProBuilderMesh>();
			pb.RebuildWithPositionsAndFaces(verts.ToArray(), faces.ToArray());

			pb.colorsInternal = cols.ToArray();
			pb.textures = uvs;

			pb.gameObject.name = t.name;

			go.transform.position = t.position;
			go.transform.localRotation = t.localRotation;
			go.transform.localScale = t.localScale;

			pb.CenterPivot(null);

			return pb;
		}

		/// <summary>
		/// ProBuilderize in-place function. You must call ToMesh() and Refresh() after
		/// returning from this function, as this only creates the pb_Object and sets its
		/// fields. This allows you to record the mesh and gameObject for Undo operations.
		/// </summary>
		/// <param name="pb"></param>
		/// <param name="preserveFaces"></param>
		/// <returns></returns>
		public static bool ResetPbObjectWithMeshFilter(ProBuilderMesh pb, bool preserveFaces)
		{
			MeshFilter mf = pb.gameObject.GetComponent<MeshFilter>();

			if (mf == null || mf.sharedMesh == null)
			{
				Log.Error(pb.name + " does not have a mesh or Mesh Filter component.");
				return false;
			}

			Mesh m = mf.sharedMesh;

			int vertexCount = m.vertexCount;
			Vector3[] m_positions = MeshUtility.GetMeshChannel<Vector3[]>(pb.gameObject, x => x.vertices);
			Color[] m_colors = MeshUtility.GetMeshChannel<Color[]>(pb.gameObject, x => x.colors);
			Vector2[] m_uvs = MeshUtility.GetMeshChannel<Vector2[]>(pb.gameObject, x => x.uv);

			List<Vector3> verts = preserveFaces ? new List<Vector3>(m.vertices) : new List<Vector3>();
			List<Color> cols = preserveFaces ? new List<Color>(m.colors) : new List<Color>();
			List<Vector2> uvs = preserveFaces ? new List<Vector2>(m.uv) : new List<Vector2>();
			List<Face> faces = new List<Face>();

			MeshRenderer mr = pb.gameObject.GetComponent<MeshRenderer>();
			if (mr == null) mr = pb.gameObject.AddComponent<MeshRenderer>();

			Material[] sharedMaterials = mr.sharedMaterials;
			int mat_length = sharedMaterials.Length;

			for (int n = 0; n < m.subMeshCount; n++)
			{
				int[] tris = m.GetTriangles(n);
				for (int i = 0; i < tris.Length; i += 3)
				{
					int index = -1;
					if (preserveFaces)
					{
						for (int j = 0; j < faces.Count; j++)
						{
							if (faces[j].distinctIndexesInternal.Contains(tris[i + 0]) ||
								faces[j].distinctIndexesInternal.Contains(tris[i + 1]) ||
								faces[j].distinctIndexesInternal.Contains(tris[i + 2]))
							{
								index = j;
								break;
							}
						}
					}

					if (index > -1 && preserveFaces)
					{
						int len = faces[index].indexesInternal.Length;
						int[] arr = new int[len + 3];
						System.Array.Copy(faces[index].indexesInternal, 0, arr, 0, len);
						arr[len + 0] = tris[i + 0];
						arr[len + 1] = tris[i + 1];
						arr[len + 2] = tris[i + 2];
						faces[index].indexesInternal = arr;
					}
					else
					{
						int[] faceTris;

						if (preserveFaces)
						{
							faceTris = new int[3]
							{
								tris[i + 0],
								tris[i + 1],
								tris[i + 2]
							};
						}
						else
						{
							verts.Add(m_positions[tris[i + 0]]);
							verts.Add(m_positions[tris[i + 1]]);
							verts.Add(m_positions[tris[i + 2]]);

							cols.Add(m_colors != null && m_colors.Length == vertexCount ? m_colors[tris[i + 0]] : Color.white);
							cols.Add(m_colors != null && m_colors.Length == vertexCount ? m_colors[tris[i + 1]] : Color.white);
							cols.Add(m_colors != null && m_colors.Length == vertexCount ? m_colors[tris[i + 2]] : Color.white);

							uvs.Add(m_uvs[tris[i + 0]]);
							uvs.Add(m_uvs[tris[i + 1]]);
							uvs.Add(m_uvs[tris[i + 2]]);

							faceTris = new int[3] { i + 0, i + 1, i + 2 };
						}

						faces.Add(
							new Face(
								faceTris,
								sharedMaterials[n >= mat_length ? mat_length - 1 : n],
								AutoUnwrapSettings.tile,
								0, // smoothing group
								-1, // texture group
								-1, // element group
								true // manualUV
							));
					}
				}
			}

			pb.positionsInternal = verts.ToArray();
			pb.texturesInternal = uvs.ToArray();
			pb.facesInternal = faces.ToArray();
			pb.sharedVertexesInternal = SharedVertex.GetSharedVertexesWithPositions(verts.ToArray());
			pb.colorsInternal = cols.ToArray();

			return true;
		}
	}
}