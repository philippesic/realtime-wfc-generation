using System;
using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
class OverlapWFC : MonoBehaviour
{
	public Training training;
	public int gridsize = 1;
	public int width = 18;
	public int depth = 18;
	public int seed = 0;
	public int N = 2;
	public bool periodicInput = false;
	public bool periodicOutput = false;
	public int symmetry = 1;
	public int foundation = 0;
	public int iterations = 0;
	public bool incremental = false;

	public OverlappingModel model;
	public GameObject[,] rendering;
	private Transform group;
	private bool undrawn = true;

	private Vector2Int windowOrigin;

	public void UpdateWindowOrigin(Vector2Int origin)
	{
		windowOrigin = origin;
	}

	void Awake()
	{
		EnsureGroup();
		Generate();
		Run();
	}

	void EnsureGroup()
	{
		if (group != null) return;
		Transform root = transform.Find("output-overlap");
		if (root == null)
		{
			var go = new GameObject("output-overlap");
			go.transform.SetParent(transform);
			go.transform.localPosition = Vector3.zero;
			root = go.transform;
		}

		group = new GameObject("OverlapOutput").transform;
		group.SetParent(root);
		group.localPosition = Vector3.zero;
		group.localRotation = Quaternion.identity;
		group.localScale = Vector3.one;
	}

	public void GenerateInitialFullWindow(Vector2Int origin)
	{
		UpdateWindowOrigin(origin);
		Generate();
		Run();
	}

	public void Generate()
	{
		if (training == null)
		{
			Debug.LogError("OverlapWFC: No Training assigned.");
			return;
		}
		if (IsPrefabRef(training.gameObject))
		{
#if UNITY_EDITOR
			GameObject o = PrefabUtility.InstantiatePrefab(training.gameObject) as GameObject;
			o.transform.position = new Vector3(0, 99999f, 0f);
			training = o.GetComponent<Training>();
#else
            GameObject o = Instantiate(training.gameObject);
            o.transform.position = new Vector3(0, 99999f, 0f);
            training = o.GetComponent<Training>();
#endif
		}
		if (training.sample == null) training.Compile();

		ClearGroup();

		EnsureGroup();
		rendering = new GameObject[width, depth];
		model = new OverlappingModel(training.sample, N, width, depth, periodicInput, periodicOutput, symmetry, foundation);
		undrawn = true;
	}

	public void Run()
	{
		if (model == null) return;
		if (!undrawn) return;

		if (model.Run(seed, iterations))
			Draw();
	}

	public void Draw()
	{
		if (group == null || model == null) return;
		undrawn = false;
		try
		{
			for (int y = 0; y < depth; y++)
			{
				for (int x = 0; x < width; x++)
				{
					if (rendering[x, y] != null) continue;

					int sample = (int)model.Sample(x, y);
					if (sample == 99) { undrawn = true; continue; }

					if (sample >= 0 && sample < training.tiles.Length)
					{
						GameObject prefab = training.tiles[sample] as GameObject;
						if (prefab == null) continue;

						Vector3 worldPos = new Vector3(
							(windowOrigin.x + x) * gridsize,
							(windowOrigin.y + y) * gridsize,
							0f);

						GameObject tile = Instantiate(prefab, worldPos, Quaternion.identity, group);

						if (training.RS != null && sample < training.RS.Length)
						{
							int rot = (int)training.RS[sample];
							tile.transform.localEulerAngles = new Vector3(0, 0, 360 - (rot * 90));
						}
						rendering[x, y] = tile;
					}
				}
			}
		}
		catch (IndexOutOfRangeException)
		{
			model = null;
		}
	}

	public GameObject GetTile(int localX, int localY)
	{
		if (rendering == null) return null;
		if (localX < 0 || localY < 0 || localX >= width || localY >= depth) return null;
		return rendering[localX, localY];
	}

	public void RegenerateAt(Vector2Int newOrigin)
	{

		UpdateWindowOrigin(newOrigin);
		ClearGroup();
		EnsureGroup();
		rendering = new GameObject[width, depth];

		model = new OverlappingModel(training.sample, N, width, depth, periodicInput, periodicOutput, symmetry, foundation);
		undrawn = true;
		Run();
	}

	void ClearGroup()
	{
		if (group != null)
		{
			if (Application.isPlaying) Destroy(group.gameObject);
			else DestroyImmediate(group.gameObject);
			group = null;
		}
	}

#if UNITY_EDITOR
	public static bool IsPrefabRef(UnityEngine.Object o)
	{
		return PrefabUtility.GetOutermostPrefabInstanceRoot(o) != null;
	}
#else
    public static bool IsPrefabRef(UnityEngine.Object o) => true;
#endif
}

