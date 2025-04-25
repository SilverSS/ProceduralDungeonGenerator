using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;
using Graphs;

/// <summary>
/// 절차적 던전 생성기 클래스
/// </summary>
[ExecuteInEditMode]
public class Generator2D : MonoBehaviour, ISerializationCallbackReceiver {
    // 셀의 타입을 정의하는 열거형
    enum CellType {
        None,       // 빈 공간
        Room,       // 방
        Hallway     // 복도
    }

    // 방을 나타내는 클래스
    class Room {
        public RectInt bounds;  // 방의 경계를 나타내는 직사각형

        // 방 생성자: 위치와 크기를 받아 방의 경계를 설정
        public Room(Vector2Int location, Vector2Int size) {
            bounds = new RectInt(location, size);
        }

        // 두 방이 서로 겹치는지 확인하는 메서드
        // a, b: 겹침을 검사할 두 방
        // 반환값: 두 방이 겹치면 true, 겹치지 않으면 false
        public static bool Intersect(Room a, Room b) {
            return !((a.bounds.position.x >= (b.bounds.position.x + b.bounds.size.x)) || 
                    ((a.bounds.position.x + a.bounds.size.x) <= b.bounds.position.x) ||
                    (a.bounds.position.y >= (b.bounds.position.y + b.bounds.size.y)) || 
                    ((a.bounds.position.y + a.bounds.size.y) <= b.bounds.position.y));
        }
    }

    [SerializeField]
    Vector2Int size;          // 던전의 전체 크기
    [SerializeField]
    int roomCount;            // 생성할 방의 개수
    [SerializeField]
    int tryMakeRoomCount;     // 방 생성 최대 시도 횟수
    [SerializeField]
    Vector2Int roomMinSize;   // 방의 최소 크기
    [SerializeField]
    Vector2Int roomMaxSize;   // 방의 최대 크기
    [SerializeField]
    GameObject cubePrefab;    // 큐브 프리팹
    [SerializeField]
    Material redMaterial;     // 방을 표시할 빨간색 재질
    [SerializeField]
    Material blueMaterial;    // 복도를 표시할 파란색 재질

    private GameObject roomsParent;  // 방들을 그룹화할 부모 오브젝트
    private GameObject pathsParent;  // 경로들을 그룹화할 부모 오브젝트

    [Header("Random Generation Options")]
    [SerializeField] private bool useRandomSeed = true;  // 랜덤 시드 사용 여부
    [SerializeField] private int seed = 0;               // 고정 시드 값
    [SerializeField] [HideInInspector] private int savedSeed = 0;  // 저장된 시드 값

    [Header("Visualization Options")]
    [SerializeField] private bool showMSTEdges = true;    // MST 간선 표시 여부
    [SerializeField] private bool showPathLines = true;    // 실제 통로 경로 표시 여부
    [SerializeField] private Color mstColor = Color.blue;  // MST 간선 색상
    [SerializeField] private Color pathColor = Color.yellow;  // 통로 경로 색상    

    Grid2D<CellType> grid;   // 2D 그리드
    List<Room> rooms;        // 생성된 방들의 목록
    [SerializeField] [HideInInspector]
    private List<List<Vector2Int>> pathLines = new List<List<Vector2Int>>();
    [SerializeField] [HideInInspector]
    private HashSet<Room> connectedRooms = new HashSet<Room>();
    [SerializeField] [HideInInspector]
    private HashSet<Prim.Edge> selectedEdges = new HashSet<Prim.Edge>();

    Random random;            // 난수 생성기
    Delaunay2D delaunay;     // 들로네 삼각분할
    
    // 공유 통로 처리를 위한 노드 클래스
    [System.Serializable]
    public class Node {
        public Vector2Int Position { get; set; }
        [SerializeField]
        private List<PathDirection> openDirections = new List<PathDirection>();
        public List<PathDirection> OpenDirections {
            get => openDirections;
            set => openDirections = value;
        }
        public int PathIndex { get; set; }

        public Node(Vector2Int position) {
            Position = position;
            OpenDirections = new List<PathDirection>();
            PathIndex = -1;
        }

        // 직렬화를 위한 기본 생성자
        public Node() {
            Position = Vector2Int.zero;
            OpenDirections = new List<PathDirection>();
            PathIndex = -1;
        }
    }

    // 방향 정보를 나타내는 열거형
    [System.Serializable]
    public enum PathDirection {
        None,       // 방향 없음
        XPlus,      // X축 양의 방향
        XMinus,     // X축 음의 방향
        YPlus,      // Y축 양의 방향
        YMinus      // Y축 음의 방향
    }
    
    // 두 위치 간의 방향을 계산하는 메서드
    private PathDirection GetDirection(Vector2Int from, Vector2Int to) {
        Vector2Int delta = to - from;
        
        if (delta.x > 0) return PathDirection.XPlus;
        if (delta.x < 0) return PathDirection.XMinus;
        if (delta.y > 0) return PathDirection.YPlus;
        if (delta.y < 0) return PathDirection.YMinus;
        
        return PathDirection.None;
    }

    // 반대 방향을 구하는 헬퍼 메서드
    private PathDirection GetOppositeDirection(PathDirection direction) {
        switch (direction) {
            case PathDirection.XPlus: return PathDirection.XMinus;
            case PathDirection.XMinus: return PathDirection.XPlus;
            case PathDirection.YPlus: return PathDirection.YMinus;
            case PathDirection.YMinus: return PathDirection.YPlus;
            default: return PathDirection.None;
        }
    }

    [SerializeField] [HideInInspector]
    private List<Node> serializedNodes = new List<Node>();  // 직렬화된 노드 목록

    [SerializeField] [HideInInspector]
    private Dictionary<Vector2Int, Node> nodes = new Dictionary<Vector2Int, Node>();  // 노드 정보를 저장할 딕셔너리

    [SerializeField] [HideInInspector]
    private Dictionary<Vector2Int, int> positionToPathIndex = new Dictionary<Vector2Int, int>();  // 위치별 경로 인덱스

    private void OnValidate() {
        // Scene 저장 시 useRandomSeed가 체크되어 있다면 현재 시드를 저장
        if (useRandomSeed && Application.isPlaying == false) {
            savedSeed = seed;
            useRandomSeed = false;
            Generate();
        }
    }

    public void OnBeforeSerialize() {
        // 직렬화 전에 실행되는 코드
    }

    public void OnAfterDeserialize() {
        // 역직렬화 후에 실행되는 코드
    }

    private void Awake() {
        if (savedSeed != 0) {
            seed = savedSeed;
            Generate();
        }
    }

    void Start() {
        if (savedSeed != 0) {
            seed = savedSeed;
        }
        Generate();
    }

    [ExecuteAlways]
    public void Generate() {
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();

        Clear();
        
        // 카테고리 부모 오브젝트 생성
        roomsParent = new GameObject("Rooms");
        roomsParent.transform.SetParent(transform);
        roomsParent.transform.position = Vector3.zero;

        pathsParent = new GameObject("Paths");
        pathsParent.transform.SetParent(transform);
        pathsParent.transform.position = Vector3.zero;
        
        // 시드 설정
        if (useRandomSeed) {
            seed = System.DateTime.Now.Millisecond;
        }
        random = new Random(seed);
        Debug.Log($"던전 생성 시드: {seed}");
        
        grid = new Grid2D<CellType>(size, Vector2Int.zero);
        rooms = new List<Room>();
        selectedEdges = new HashSet<Prim.Edge>();
        connectedRooms = new HashSet<Room>();
        pathLines = new List<List<Vector2Int>>();
        nodes = new Dictionary<Vector2Int, Node>();
        positionToPathIndex = new Dictionary<Vector2Int, int>();
        serializedNodes = new List<Node>();

        PlaceRooms();
        Triangulate();
        CreateHallways();
        PathfindHallways();

        // 노드 정보 직렬화
        serializedNodes = new List<Node>(nodes.Values);

        stopwatch.Stop();

        if (Application.isPlaying == false)
        {
            string time = stopwatch.ElapsedMilliseconds / 1000.0f < 1 ? $"{stopwatch.ElapsedMilliseconds}ms" : $"{stopwatch.ElapsedMilliseconds / 1000.0f}s";
            Debug.Log($"던전 생성 완료: {time}");
        }
    }

    [ExecuteAlways]
    public void Clear() {
        while (transform.childCount > 0) {
            DestroyImmediate(transform.GetChild(0).gameObject);
        }

        if (grid != null) grid = null;
        if (rooms != null) rooms.Clear();
        if (delaunay != null) delaunay = null;
        if (selectedEdges != null) selectedEdges.Clear();
        if (connectedRooms != null) connectedRooms.Clear();
        if (pathLines != null) pathLines.Clear();
        if (nodes != null) nodes.Clear();
        if (positionToPathIndex != null) positionToPathIndex.Clear();
    }

    // 방 배치 메서드
    void PlaceRooms() {
        int createdRooms = 0;

        for (int i = 0; i < tryMakeRoomCount && createdRooms < roomCount; i++) {
            // 랜덤한 위치 생성
            Vector2Int location = new Vector2Int(
                random.Next(0, size.x),
                random.Next(0, size.y)
            );

            // 랜덤한 방 크기 생성
            Vector2Int roomSize = new Vector2Int(
                random.Next(roomMinSize.x, roomMaxSize.x + 1),
                random.Next(roomMinSize.y, roomMaxSize.y + 1)
            );

            bool add = true;
            Room newRoom = new Room(location, roomSize);
            // 방 주변에 1칸의 여유 공간을 둔 버퍼 영역 생성
            Room buffer = new Room(location + new Vector2Int(-1, -1), roomSize + new Vector2Int(2, 2));

            // 다른 방들과의 겹침 검사
            foreach (var room in rooms) {
                if (Room.Intersect(room, buffer)) {
                    add = false;
                    break;
                }
            }

            // 던전 경계 검사
            if (newRoom.bounds.xMin < 0 || newRoom.bounds.xMax >= size.x
                || newRoom.bounds.yMin < 0 || newRoom.bounds.yMax >= size.y) {
                add = false;
            }

            // 방 추가가 가능한 경우
            if (add) {
                rooms.Add(newRoom);
                PlaceRoom(newRoom.bounds.position, newRoom.bounds.size);
                
                // 그리드에 방 영역 표시
                foreach (var pos in newRoom.bounds.allPositionsWithin) {
                    grid[pos] = CellType.Room;
                }
                createdRooms++;
            }
        }

        if (createdRooms < roomCount) {
            Debug.LogWarning($"요청한 방 개수({roomCount})보다 적은 수의 방({createdRooms})이 생성되었습니다.");
        }
    }

    // 들로네 삼각분할 수행 메서드
    void Triangulate() {
        List<Vertex> vertices = new List<Vertex>();

        // 각 방의 중심점을 정점으로 변환
        foreach (var room in rooms) {
            vertices.Add(new Vertex<Room>((Vector2)room.bounds.position + ((Vector2)room.bounds.size) / 2, room));
        }

        delaunay = Delaunay2D.Triangulate(vertices);
    }

    // 복도 생성을 위한 간선 선택 메서드
    void CreateHallways() {
        connectedRooms = new HashSet<Room>();
        selectedEdges = new HashSet<Prim.Edge>();
        List<Prim.Edge> edges = new List<Prim.Edge>();

        // 들로네 삼각분할의 모든 간선을 프림 알고리즘용 간선으로 변환
        foreach (var edge in delaunay.Edges) {
            edges.Add(new Prim.Edge(edge.U, edge.V));
        }

        if (edges.Count > 0 && rooms.Count > 0) {
            var mst = Prim.MinimumSpanningTree(edges, edges[0].U);
            
            if (mst != null) {
                foreach (var edge in mst) {
                    if (edge?.U is Vertex<Room> u && edge?.V is Vertex<Room> v) {
                        selectedEdges.Add(edge);
                        connectedRooms.Add(u.Item);
                        connectedRooms.Add(v.Item);
                    }
                }
            }

            // 연결되지 않은 방 처리
            var unconnectedRooms = new HashSet<Room>(rooms);
            unconnectedRooms.ExceptWith(connectedRooms);

            foreach (var room in unconnectedRooms) {
                Room nearestRoom = FindNearestConnectedRoom(room);
                if (nearestRoom != null) {
                    var edge = new Prim.Edge(
                        new Vertex<Room>((Vector2)room.bounds.position + ((Vector2)room.bounds.size) / 2, room),
                        new Vertex<Room>((Vector2)nearestRoom.bounds.position + ((Vector2)nearestRoom.bounds.size) / 2, nearestRoom)
                    );
                    selectedEdges.Add(edge);
                    connectedRooms.Add(room);
                }
            }
        }

        // 12.5% 확률로 추가 간선 선택 (순환 경로 생성)
        var remainingEdges = new HashSet<Prim.Edge>(edges);
        remainingEdges.ExceptWith(selectedEdges);

        foreach (var edge in remainingEdges) {
            if (random.NextDouble() < 0.125) {
                selectedEdges.Add(edge);
            }
        }
    }

    // 가장 가까운 연결된 방 찾기
    private Room FindNearestConnectedRoom(Room room) {
        Room nearest = null;
        float minDistance = float.MaxValue;
        
        foreach (var connectedRoom in connectedRooms) {
            float distance = Vector2.Distance(
                room.bounds.center,
                connectedRoom.bounds.center
            );
            
            if (distance < minDistance) {
                minDistance = distance;
                nearest = connectedRoom;
            }
        }
        
        return nearest;
    }

    // A* 알고리즘을 사용한 경로 탐색 메서드
    void PathfindHallways() {
        DungeonPathfinder2D aStar = new DungeonPathfinder2D(size);
        int currentPathIndex = 0;

        foreach (var edge in selectedEdges) {
            // 시작점과 도착점 설정
            var startRoom = (edge.U as Vertex<Room>).Item;
            var endRoom = (edge.V as Vertex<Room>).Item;

            // 방 가장자리 위치 계산
            var startPos = GetRoomEdgePosition(startRoom, endRoom, true);
            var endPos = GetRoomEdgePosition(endRoom, startRoom, false);

            Debug.Log($"경로 탐색 시작: 시작점 [{startPos.x},{startPos.y}] -> 끝점 [{endPos.x},{endPos.y}]");

            // A* 경로 탐색
            var path = aStar.FindPath(startPos, endPos, (DungeonPathfinder2D.Node a, DungeonPathfinder2D.Node b) => {
                var pathCost = new DungeonPathfinder2D.PathCost();
                
                pathCost.cost = Vector2Int.Distance(b.Position, endPos);    // 휴리스틱 비용

                // 셀 타입에 따른 이동 비용 설정
                if (grid[b.Position] == CellType.Room) {
                    pathCost.cost += 10;    // 방을 통과하는 비용
                } else if (grid[b.Position] == CellType.None) {
                    pathCost.cost += 5;     // 빈 공간을 통과하는 비용
                } else if (grid[b.Position] == CellType.Hallway) {
                    pathCost.cost += 1;     // 기존 복도를 통과하는 비용
                }

                pathCost.traversable = true;  // 모든 셀은 통과 가능

                return pathCost;
            });

            // 경로가 존재하는 경우 복도 생성
            if (path != null) {
                // 목표 방의 외곽에 처음 닿는 지점을 찾아 경로를 자릅니다
                int endIndex = path.Count;
                for (int i = 0; i < path.Count; i++) {
                    var pos = path[i];
                    // 현재 위치가 목표 방 안에 있는지 확인
                    if (IsPositionInRoom(pos, endRoom)) {
                        // 현재 위치(외곽)까지 포함
                        endIndex = i + 1;
                        break;
                    }
                }

                // 경로를 외곽 지점까지 사용
                path = path.GetRange(0, endIndex);
                
                // 경로 저장
                pathLines.Add(path);
                
                // 먼저 모든 위치를 복도로 표시
                foreach (var pos in path) {
                    if (grid[pos] == CellType.None) {
                        grid[pos] = CellType.Hallway;
                    }
                    positionToPathIndex[pos] = currentPathIndex;
                }

                // 노드 정보 생성 및 방향 설정
                for (int i = 0; i < path.Count; i++) {
                    var current = path[i];

                    // 노드 정보 가져오기 또는 생성
                    if (!nodes.TryGetValue(current, out Node node)) {
                        node = new Node(current);
                        nodes[current] = node;
                    }

                    node.PathIndex = currentPathIndex;

                    // 이전 노드와의 방향 설정
                    if (i > 0) {
                        var prev = path[i - 1];
                        var direction = GetDirection(prev, current);
                        var oppositeDirection = GetOppositeDirection(direction);
                        if (!node.OpenDirections.Contains(oppositeDirection)) {
                            node.OpenDirections.Add(oppositeDirection);
                        }
                    }

                    // 다음 노드와의 방향 설정
                    if (i < path.Count - 1) {
                        var next = path[i + 1];
                        var direction = GetDirection(current, next);
                        if (!node.OpenDirections.Contains(direction)) {
                            node.OpenDirections.Add(direction);
                        }
                    }
                }

                // 복도 시각화
                foreach (var pos in path) {
                    if (grid[pos] == CellType.Hallway) {
                        PlaceHallway(pos);
                    }
                }
                
                currentPathIndex++;
            }
        }
    }
    
    // 위치가 방 안에 있는지 확인
    private bool IsPositionInRoom(Vector2Int position, Room room) {
        return position.x >= room.bounds.xMin && position.x < room.bounds.xMax && 
               position.y >= room.bounds.yMin && position.y < room.bounds.yMax;
    }
    
    // 방의 가장자리 위치를 계산하는 메서드 (3D와 유사하게 구현)
    private Vector2Int GetRoomEdgePosition(Room room, Room targetRoom, bool isStart) {
        Vector2Int roomCenter = new Vector2Int(
            (int)room.bounds.center.x,
            (int)room.bounds.center.y
        );

        Vector2Int targetCenter = new Vector2Int(
            (int)targetRoom.bounds.center.x,
            (int)targetRoom.bounds.center.y
        );

        Vector2Int edgePos;
        
        if (isStart) {
            // 시작점: 현재 방에서 목표 방으로 향하는 방향으로 가장자리 찾기
            Vector2 direction = ((Vector2)(targetCenter - roomCenter)).normalized;
            
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y)) {
                // X 방향이 주된 이동 방향
                edgePos = new Vector2Int(
                    direction.x > 0 ? room.bounds.xMax - 1 : room.bounds.xMin,
                    roomCenter.y
                );
            } else {
                // Y 방향이 주된 이동 방향
                edgePos = new Vector2Int(
                    roomCenter.x,
                    direction.y > 0 ? room.bounds.yMax - 1 : room.bounds.yMin
                );
            }
        } else {
            // 끝점: 상대 방에서 가장 가까운 현재 방의 가장자리 찾기
            Vector2Int closestEdge = Vector2Int.zero;
            float minDistance = float.MaxValue;

            // X축 가장자리 검사
            int[] xPoints = new int[] { room.bounds.xMin, room.bounds.xMax - 1 };
            foreach (int x in xPoints) {
                for (int y = room.bounds.yMin; y < room.bounds.yMax; y++) {
                    Vector2Int pos = new Vector2Int(x, y);
                    float dist = Vector2.Distance(pos, targetCenter);
                    if (dist < minDistance) {
                        minDistance = dist;
                        closestEdge = pos;
                    }
                }
            }

            // Y축 가장자리 검사
            int[] yPoints = new int[] { room.bounds.yMin, room.bounds.yMax - 1 };
            foreach (int y in yPoints) {
                for (int x = room.bounds.xMin; x < room.bounds.xMax; x++) {
                    Vector2Int pos = new Vector2Int(x, y);
                    float dist = Vector2.Distance(pos, targetCenter);
                    if (dist < minDistance) {
                        minDistance = dist;
                        closestEdge = pos;
                    }
                }
            }

            edgePos = closestEdge;
        }

        Debug.Log($"방 가장자리 위치 계산: [{edgePos.x},{edgePos.y}] (시작점: {isStart})");
        return edgePos;
    }

    // 3D 큐브를 생성하는 메서드
    void PlaceCube(Vector2Int location, Vector2Int size, Material material) {
        GameObject go = Instantiate(cubePrefab, new Vector3(location.x, 0, location.y), Quaternion.identity);
        go.transform.SetParent(transform);
        go.GetComponent<Transform>().localScale = new Vector3(size.x, 1, size.y);
        go.GetComponent<MeshRenderer>().material = material;
    }

    // 방을 시각화하는 메서드
    void PlaceRoom(Vector2Int location, Vector2Int size) {
        GameObject roomObj = new GameObject($"Room-{rooms.Count}-[{location.x},{location.y}]");
        roomObj.transform.SetParent(roomsParent.transform);
        roomObj.transform.position = new Vector3(location.x, 0, location.y);
        
        GameObject cube = Instantiate(cubePrefab, new Vector3(location.x, 0, location.y), Quaternion.identity);
        cube.transform.SetParent(roomObj.transform);
        cube.transform.localScale = new Vector3(size.x, 1, size.y);
        cube.GetComponent<MeshRenderer>().material = redMaterial;
    }

    // 복도를 시각화하는 메서드
    void PlaceHallway(Vector2Int location) {
        int pathIndex = positionToPathIndex[location];
        GameObject pathObj = GameObject.Find($"Path-{pathIndex}");
        
        if (pathObj == null) {
            pathObj = new GameObject($"Path-{pathIndex}");
            pathObj.transform.SetParent(pathsParent.transform);
            pathObj.transform.position = Vector3.zero;
        }
        
        GameObject cube = Instantiate(cubePrefab, new Vector3(location.x, 0, location.y), Quaternion.identity);
        cube.name = $"Hallway-[{location.x},{location.y}]";
        cube.transform.SetParent(pathObj.transform);
        cube.transform.localScale = Vector3.one;
        cube.GetComponent<MeshRenderer>().material = blueMaterial;
    }

    // 경로 시각화 - 방향 화살표와 시작/끝점 표시 추가
    private void OnDrawGizmos() {
        if (selectedEdges == null) return;

        if (showMSTEdges) {
            Gizmos.color = mstColor;
            foreach (var edge in selectedEdges) {
                if (edge?.U is Vertex<Room> u && edge?.V is Vertex<Room> v) {
                    Vector3 start = new Vector3(u.Item.bounds.center.x, 0, u.Item.bounds.center.y) + Vector3.one * 0.5f;
                    Vector3 end = new Vector3(v.Item.bounds.center.x, 0, v.Item.bounds.center.y) + Vector3.one * 0.5f;
                    Gizmos.DrawLine(start, end);

                    // 방향 표시를 위한 구체 추가
                    Gizmos.DrawSphere(start, 0.2f);
                    Gizmos.DrawSphere(end, 0.2f);
                }
            }
        }

        if (showPathLines && pathLines != null) {
            Gizmos.color = pathColor;
            foreach (var path in pathLines) {
                for (int i = 1; i < path.Count; i++) {
                    Vector3 start = new Vector3(path[i-1].x, 0, path[i-1].y) + Vector3.one * 0.5f;
                    Vector3 end = new Vector3(path[i].x, 0, path[i].y) + Vector3.one * 0.5f;
                    Gizmos.DrawLine(start, end);

                    // 방향 표시를 위한 구체 추가
                    Gizmos.DrawSphere(start, 0.2f);
                    Gizmos.DrawSphere(end, 0.2f);
                    
                    // 경로의 시작점과 끝점을 다른 색상으로 표시
                    if (i == 1) {
                        Gizmos.color = Color.green;
                        Gizmos.DrawSphere(start, 0.3f);
                        Gizmos.color = pathColor;
                    }
                    if (i == path.Count - 1) {
                        Gizmos.color = Color.red;
                        Gizmos.DrawSphere(end, 0.3f);
                        Gizmos.color = pathColor;
                    }
                }
            }
        }
        
        // 노드의 열린 방향 시각화
        if (nodes != null) {
            foreach (var node in nodes.Values) {
                if (node.OpenDirections.Count == 0) continue;

                Vector3 center = new Vector3(node.Position.x, 0, node.Position.y) + Vector3.one * 0.5f;
                float arrowLength = 0.3f;
                float arrowHeadLength = 0.1f;
                float arrowHeadAngle = 20.0f;

                foreach (var direction in node.OpenDirections) {
                    Vector3 dir = Vector3.zero;
                    Color color = Color.white;

                    switch (direction) {
                        case PathDirection.XPlus:
                            dir = Vector3.right;
                            color = Color.red;
                            break;
                        case PathDirection.XMinus:
                            dir = Vector3.left;
                            color = new Color(1f, 0.5f, 0.5f);
                            break;
                        case PathDirection.YPlus:
                            dir = Vector3.forward;
                            color = Color.white;
                            break;
                        case PathDirection.YMinus:
                            dir = Vector3.back;
                            color = Color.black;
                            break;
                    }

                    // 인접 셀 확인
                    Vector2Int neighborPos = node.Position + new Vector2Int(
                        direction == PathDirection.XPlus ? 1 : direction == PathDirection.XMinus ? -1 : 0,
                        direction == PathDirection.YPlus ? 1 : direction == PathDirection.YMinus ? -1 : 0
                    );

                    // 인접 셀이 없는 경우 다른 색상으로 표시
                    if (grid != null) {
                        if (!grid.InBounds(neighborPos) || grid[neighborPos] == CellType.None) {
                            color = Color.yellow;
                        }
                    }

                    Gizmos.color = color;
                    Vector3 end = center + dir * arrowLength;
                    
                    // 화살표 몸통
                    Gizmos.DrawLine(center, end);
                    
                    // 화살표 머리
                    Vector3 right = Quaternion.LookRotation(dir) * Quaternion.Euler(0, 180 + arrowHeadAngle, 0) * Vector3.forward;
                    Vector3 left = Quaternion.LookRotation(dir) * Quaternion.Euler(0, 180 - arrowHeadAngle, 0) * Vector3.forward;
                    Gizmos.DrawLine(end, end + right * arrowHeadLength);
                    Gizmos.DrawLine(end, end + left * arrowHeadLength);
                }
            }
        }
    }
}
