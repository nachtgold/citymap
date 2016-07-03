using UnityEngine;
using System.Collections.Generic;
using System;

public class World : MonoBehaviour {

    int height;
    int width;

    [Range(1, 100)]
    public int zoneCount = 10;

    public string seed;

    private List<Zone> zones = new List<Zone>();

    void Start() {
        float windowaspect = (float)Screen.width / (float)Screen.height;
        height = (int)Camera.main.orthographicSize * 2;
        width = (int)Mathf.Floor(height * windowaspect);

        GenerateZones();
        GenerateMap();
        FindEdges();
    }

    void FindEdges() {
        foreach (Zone zone in zones) {
            for (int x = zone.x; x < width; x++) {
                for (int y = zone.y; y < height; y++) {
                    if (zone.map[x, y] == 1) {
                        bool isAnEdge = IsCoordAnEdge(zone.map, x, y);

                        if (isAnEdge) {
                            zone.edges.Add(new Vector2(x, y));
                            FollowZoneEdge(zone, x, y, out x, out y);
                        }
                    }
                }
            }
        }
    }

    bool IsCoordAnEdge(int[,] map, int x, int y) {
        for (int tileX = x - 1; tileX <= x + 1; tileX++) {
            for (int tileY = y - 1; tileY <= y + 1; tileY++) {
                if ((tileX == x || tileY == y) && (!IsOnMap(tileX, tileY) || map[tileX, tileY] == 0)) {
                    return true;
                }
            }
        }

        return false;
    }

    void FollowZoneEdge(Zone zone, int x, int y, out int nx, out int ny) {
        nx = x;
        ny = y;

        // horizontal and vertical
        for (int tileX = x - 1; tileX <= x + 1; tileX++) {
            for (int tileY = y - 1; tileY <= y + 1; tileY++) {
                if ((tileX == x || tileY == y) && IsOnMap(tileX, tileY) && zone.map[tileX, tileY] == 1 && 
                    !zone.edges.Contains(new Vector2(tileX, tileY)) && IsCoordAnEdge(zone.map, tileX, tileY)) {

                    zone.edges.Add(new Vector2(tileX, tileY));
                    FollowZoneEdge(zone, tileX, tileY, out nx, out ny);
                    return;
                }
            }
        }

        // diagonal
        for (int tileX = x - 1; tileX <= x + 1; tileX++) {
            for (int tileY = y - 1; tileY <= y + 1; tileY++) {
                if (tileX != x && tileY != y && IsOnMap(tileX, tileY) && zone.map[tileX, tileY] == 1 &&
                    !zone.edges.Contains(new Vector2(tileX, tileY)) && IsCoordAnEdge(zone.map, tileX, tileY)) {

                    zone.edges.Add(new Vector2(tileX, tileY));
                    FollowZoneEdge(zone, tileX, tileY, out nx, out ny);
                    return;
                }
            }
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

    void OnDrawGizmos() {
        foreach (Zone zone in zones) {
            Gizmos.color = Color.black;
            Gizmos.DrawCube(GetWorldVectorFor(zone.x, zone.y), Vector3.one*2);

            Gizmos.color = zone.color;
            foreach (Vector2 coord in zone.edges) {
                Gizmos.DrawCube(GetWorldVectorFor(coord.x, coord.y), Vector3.one);
            }
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
        public HashSet<Vector2> edges = new HashSet<Vector2>();

        public Zone(int worldWidth, int worldHeight, int _x, int _y, Color _color) {
            this.map = new int[worldWidth, worldHeight];
            this.x = _x;
            this.y = _y;
            this.color = _color;
        }
    }

    Vector3 GetWorldVectorFor(double x, double y) {
        return new Vector3(-width / 2 + ((float)x), -height / 2 + ((float)y), 0);
    }

    Vector3 GetWorldVectorFor(int x, int y) {
        return new Vector3(-width / 2 + x, -height / 2 + y, 0);
    }
}
