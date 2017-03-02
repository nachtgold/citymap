using UnityEngine;
using System.Collections.Generic;
using System;

public class World : MonoBehaviour {

    int height;
    int width;

    [Range(1, 100)]
    public int zoneCount = 10;

    public string seed;
    public Transform zonePrefap;

    private List<Zone> zones = new List<Zone>();

    void Start() {
        float windowaspect = (float)Screen.width / (float)Screen.height;
        height = (int)Camera.main.orthographicSize * 2;
        width = (int)Mathf.Floor(height * windowaspect);

        GenerateZones();
        GenerateMap();
        FindEdges();
        FindVertices();
        TriangulateZones();
    }

    void TriangulateZones() {
        for (int i = 0; i< zones.Count; ++i) {
            Zone zone = zones[i];
            Vector2[] vertices2D = zone.vertices.ToArray();

            Triangulator triangulator = new Triangulator(vertices2D);
            int[] indices = triangulator.Triangulate();

            Vector3[] vertices = new Vector3[vertices2D.Length];
            for (int j = 0; j < vertices.Length; j++) {
                vertices[j] = GetWorldVectorFor(vertices2D[j]);
                vertices2D[j] = GetWorldVector2For(vertices2D[j]);
            }

            Mesh msh = new Mesh();
            msh.vertices = vertices;
            msh.triangles = indices;
            msh.RecalculateNormals();
            msh.RecalculateBounds();

            Transform clone = Instantiate(zonePrefap);
            clone.parent = transform;
            clone.name = "Zone " + i;
            clone.GetComponent<MeshFilter>().mesh = msh;
            
            clone.GetComponent<MeshRenderer>().material.color = zone.color;
            clone.GetComponent<PolygonCollider2D>().SetPath(0, vertices2D);
        }
    }

    void FindVertices() {
        foreach (Zone zone in zones) {
            if (zone.edges.Count > 1) {
                zone.vertices.Add(zone.edges[0]);

                for (int i = 1; i < zone.edges.Count - 1; i++) {
                    Vector2 previousCoord = zone.edges[i - 1];
                    Vector2 interestingCoord = zone.edges[i];
                    Vector2 followingCoord = zone.edges[i + 1];

                    // it is an vertice, when the relative position between the previous and following node is different
                    if (previousCoord.x - interestingCoord.x != interestingCoord.x - followingCoord.x ||
                        previousCoord.y - interestingCoord.y != interestingCoord.y - followingCoord.y) {
                        zone.vertices.Add(interestingCoord);
                    }
                }

                zone.vertices.Add(zone.edges[zone.edges.Count - 1]);

                // first vertice is on an edge between two other vertices
                int len = zone.vertices.Count;
                if (len > 2) {
                    if (zone.edges[len - 1].x - zone.edges[0].x == zone.edges[0].x - zone.edges[1].x &&
                        zone.edges[len - 1].y - zone.edges[0].y == zone.edges[0].y - zone.edges[1].y) {

                        zone.vertices.RemoveAt(0);
                    }
                }
            }
        }
    }

    void FindEdges() {
        foreach (Zone zone in zones) {
            bool edgesNotFound = true;
            for (int x = zone.x; edgesNotFound && x < width; x++) {
                for (int y = zone.y; edgesNotFound && y < height; y++) {
                    if (zone.map[x, y] == 1 && !zone.edges.Contains(new Vector2(x, y))) {

                        int edgeCount;
                        bool isAnEdge = IsCoordAnEdge(zone.map, x, y, out edgeCount);
                        if (isAnEdge) {
                            zone.edges.Add(new Vector2(x, y));
                            FollowZoneEdge(zone, x, y, out x, out y);
                            // corner pixel are catched by edge detection, but thats ok 
                            edgesNotFound = false;
                        }
                    }
                }
            }
        }
    }

    bool IsCoordAnEdge(int[,] map, int x, int y, out int edgeCount) {
        edgeCount = 0;
        for (int tileX = x - 1; tileX <= x + 1; tileX++) {
            for (int tileY = y - 1; tileY <= y + 1; tileY++) {
                if ((tileX == x || tileY == y) && (!IsOnMap(tileX, tileY) || map[tileX, tileY] == 0)) {
                    edgeCount++;
                }
            }
        }

        return edgeCount > 0;
    }

    void FollowZoneEdge(Zone zone, int x, int y, out int nx, out int ny) {
        nx = x;
        ny = y;

        // best edge for isolated pixels at the end of a corner
        int bestX = -1;
        int bestY = -1;
        bool edgeFound = false;

        // horizontal and vertical
        for (int tileX = x - 1; tileX <= x + 1; tileX++) {
            for (int tileY = y - 1; tileY <= y + 1; tileY++) {
                int edgeCount;
                if ((tileX == x || tileY == y) && IsOnMap(tileX, tileY) && zone.map[tileX, tileY] == 1 &&
                    !zone.edges.Contains(new Vector2(tileX, tileY)) && IsCoordAnEdge(zone.map, tileX, tileY, out edgeCount)) {

                    if (!edgeFound || edgeCount > 2) {
                        bestX = tileX;
                        bestY = tileY;
                        edgeFound = true;
                    }
                }
            }
        }

        // diagonal
        for (int tileX = x - 1; tileX <= x + 1; tileX++) {
            for (int tileY = y - 1; tileY <= y + 1; tileY++) {
                int edgeCount;
                if (tileX != x && tileY != y && IsOnMap(tileX, tileY) && zone.map[tileX, tileY] == 1 &&
                    !zone.edges.Contains(new Vector2(tileX, tileY)) && IsCoordAnEdge(zone.map, tileX, tileY, out edgeCount)) {

                    if (!edgeFound || edgeCount > 2) {
                        bestX = tileX;
                        bestY = tileY;
                        edgeFound = true;
                    }
                }
            }
        }

        if (edgeFound) {
            zone.edges.Add(new Vector2(bestX, bestY));
            FollowZoneEdge(zone, bestX, bestY, out nx, out ny);
        }
    }

    bool IsOnMap(int x, int y) {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    void GenerateMap() {
        double[] xs;
        double[] ys;
        Color[] colors;
        MapZonesIntoLists(out xs, out ys, out colors);

        int n = 0;
        for (int x = 0; x < width; x++) {
            for (int y = 0; y < height; y++) {
                n = 0;
                for (byte i = 0; i < zones.Count; i++) {
                    if (distance(zones[i].x, x, zones[i].y, y) < distance(zones[n].x, x, zones[n].y, y)) {
                        n = i;
                    }
                }

                zones[n].map[x, y] = 1;
            }
        }
    }

    void MapZonesIntoLists(out double[] xs, out double[] ys, out Color[] colors) {
        xs = new double[zones.Count];
        ys = new double[zones.Count];
        colors = new Color[zones.Count];

        for (int i = 0; i < zones.Count; ++i) {
            Zone zone = zones[i];
            xs[i] = zone.x;
            ys[i] = zone.y;
            colors[i] = zone.color;
        }
    }

    void GenerateZones() {
        UnityEngine.Random.seed = seed.GetHashCode();
        for (int i = 0; i < zoneCount; ++i) {
            int x = UnityEngine.Random.Range(0, width);
            int y = UnityEngine.Random.Range(0, height);
            Zone zone = new Zone(width, height, x, y, new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value));
            zones.Add(zone);
        }
    }
    
    double distance(int x1, int x2, int y1, int y2) {
        return Math.Abs(x1 - x2) + Math.Abs(y1 - y2);
    }

    public class Zone {
        public int x;
        public int y;
        public Color color;

        public int[,] map;
        public List<Vector2> edges = new List<Vector2>();
        public List<Vector2> vertices = new List<Vector2>();

        public Zone(int worldWidth, int worldHeight, int _x, int _y, Color _color) {
            this.map = new int[worldWidth, worldHeight];
            this.x = _x;
            this.y = _y;
            this.color = _color;
        }
    }

    Vector3 GetWorldVectorFor(Vector2 vector) {
        return new Vector3(-width / 2 + vector.x, -height / 2 + vector.y, 0);
    }

    Vector3 GetWorldVector2For(Vector2 vector) {
        return new Vector2(-width / 2 + vector.x, -height / 2 + vector.y);
    }

    Vector3 GetWorldVectorFor(float x, float y) {
        return new Vector3(-width / 2 + x, -height / 2 + y, 0);
    }

    Vector3 GetWorldVectorFor(float x, float y, float z) {
        return new Vector3(-width / 2 + x, -height / 2 + y, z);
    }
}
