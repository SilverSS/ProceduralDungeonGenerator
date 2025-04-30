using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;
using Graphs;
using System;
using UnityEngine.Rendering;
using System.Linq;

/// <summary>
/// 3D 절차적 던전 생성기 클래스
/// </summary>
[ExecuteInEditMode]
public class Generator3D : MonoBehaviour, ISerializationCallbackReceiver {
    // 셀의 타입을 정의하는 열거형
    enum CellType {
        None,       // 빈 공간
        Room,       // 방
        Hallway,    // 복도
        Stairs      // 계단
    }

    [SerializeField]
    Vector3Int size;    // 던전의 3차원 크기
    [SerializeField]
    int roomCount;      // 생성할 방의 개수
    [SerializeField]
    int tryMakeRoomCount;   // 방 생성 시도 횟수
    [SerializeField]
    Vector3Int roomMinSize; // 방의 최소 3차원 크기
    [SerializeField]
    Vector3Int roomMaxSize; // 방의 최대 3차원 크기
    [SerializeField]
    GameObject cubePrefab;  // 큐브 프리팹
    [SerializeField]
    Material normalRoomMaterial;    // 방을 표시할 빨간색 재질
    [SerializeField]
    Material startRoomMaterial;     // 시작 방을 표시할 빨간색 재질
    [SerializeField]
    Material endRoomMaterial;       // 종료 방을 표시할 빨간색 재질
    [SerializeField]
    Material merchantRoomMaterial;  // 상인 방을 표시할 빨간색 재질    
    [SerializeField]
    Material hallwayMaterial;       // 복도를 표시할 파란색 재질
    [SerializeField]
    Material stairMaterial;         // 계단을 표시할 초록색 재질

    // 카테고리 오브젝트
    private GameObject roomsCategory;
    private GameObject pathsCategory;
    private GameObject tempParent;  // 임시 부모 오브젝트
    private int roomCounter = 0;
    private Dictionary<Vector3Int, GameObject> stairGroups = new Dictionary<Vector3Int, GameObject>();
    private Dictionary<int, GameObject> pathGroups = new Dictionary<int, GameObject>();
    private Dictionary<Vector3Int, int> positionToPathIndex = new Dictionary<Vector3Int, int>();

    [SerializeField] [HideInInspector]
    Grid3D<CellType> grid;      // 3D 그리드
    [SerializeField] [HideInInspector] 
    List<Room3D> rooms;           // 생성된 방들의 목록
    
    [SerializeField] [HideInInspector] 
    HashSet<Prim.Edge> selectedEdges = new HashSet<Prim.Edge>();  // 선택된 간선들
    [SerializeField] [HideInInspector] 
    private List<List<Vector3Int>> pathLines = new List<List<Vector3Int>>();    // 그리드 단위 경로
    [SerializeField] [HideInInspector] 
    HashSet<Room3D> connectedRooms = new HashSet<Room3D>();  // 연결된 방들

    
    Random random;            // 난수 생성기
    Delaunay3D delaunay;     // 3D 들로네 삼각분할

    [Header("Random Generation Options")]
    [SerializeField] private bool useRandomSeed = true;  // 랜덤 시드 사용 여부
    [SerializeField] private int seed = 0;               // 고정 시드 값
    [SerializeField] [HideInInspector] private int savedSeed = 0;  // 저장된 시드 값

    [Header("Visualization Options")]
    [SerializeField] private bool showMSTEdges = true;    // MST 간선 표시 여부
    [SerializeField] private Color mstColor = Color.blue;  // MST 간선 색상
    [SerializeField] private Color pathColor = Color.yellow;  // 통로 경로 색상

    [Header("Room Properties")]
    [SerializeField] private DungeonGenerationOptions generationOptions = new DungeonGenerationOptions();

    private Room3D startRoom;
    private Room3D exitRoom;
    private Room3D merchantRoom;


    [Header("Room Cell Prefabs")]
    [SerializeField]
    GameObject wallPrefab;      // 벽 프리팹
    [SerializeField]
    GameObject entrancePrefab;  // 기본 출입구 프리팹
    [SerializeField]
    GameObject ceilingPrefab;   // 천정 프리팹
    [SerializeField]
    GameObject floorPrefab;     // 바닥 프리팹

    [Header("Grid Settings")]
    [SerializeField]
    private float UnitPerGrid = 5f;  // 그리드 1유닛당 실제 유니티 월드 크기

    // Node 클래스에 방향 정보 추가
    [System.Serializable]
    public class Node {
        public Vector3Int Position { get; set; }
        [SerializeField]
        private List<PathDirection> outerDirections = new List<PathDirection>();
        public List<PathDirection> OuterDirections {
            get => outerDirections;
            set => outerDirections = value;
        }
        public bool IsStair { get; set; }
        public int PathIndex { get; set; }

        public Node(Vector3Int position) {
            Position = position;
            OuterDirections = new List<PathDirection>();
            IsStair = false;
            PathIndex = -1;
        }

        // 직렬화를 위한 기본 생성자
        public Node() {
            Position = Vector3Int.zero;
            OuterDirections = new List<PathDirection>();
            IsStair = false;
            PathIndex = -1;
        }
    }


    [SerializeField] [HideInInspector]
    private List<Node> serializedNodes = new List<Node>();  // 직렬화된 노드 목록

    [SerializeField] [HideInInspector]
    private Dictionary<Vector3Int, Node> nodes = new Dictionary<Vector3Int, Node>();  // 노드 정보를 저장할 딕셔너리

    // 두 위치 간의 방향을 계산하는 메서드
    private PathDirection GetDirection(Vector3Int from, Vector3Int to) {
        Vector3Int delta = to - from;
        
        if (delta.x > 0) return PathDirection.XPlus;
        if (delta.x < 0) return PathDirection.XMinus;
        if (delta.z > 0) return PathDirection.ZPlus;
        if (delta.z < 0) return PathDirection.ZMinus;
        
        return PathDirection.None;
    }

    // 반대 방향을 구하는 헬퍼 메서드
    private PathDirection GetOppositeDirection(PathDirection direction) {
        switch (direction) {
            case PathDirection.XPlus: return PathDirection.XMinus;
            case PathDirection.XMinus: return PathDirection.XPlus;
            case PathDirection.ZPlus: return PathDirection.ZMinus;
            case PathDirection.ZMinus: return PathDirection.ZPlus;
            default: return PathDirection.None;
        }
    }

    // 게임 시작 시 호출되는 메서드
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

        Clear();  // 기존 던전 데이터 초기화
        CreateCategoryObjects();  // 카테고리 오브젝트 생성

        // 시드 설정
        if (useRandomSeed) {
            seed = System.DateTime.Now.Millisecond;
        }
        random = new Random(seed);
        Debug.Log($"던전 생성 시드: {seed}");

        grid = new Grid3D<CellType>(size, Vector3Int.zero);   // 3D 그리드 초기화
        rooms = new List<Room3D>();                             // 방 목록 초기화
        nodes = new Dictionary<Vector3Int, Node>();           // 노드 정보 초기화
        serializedNodes = new List<Node>();                   // 직렬화된 노드 목록 초기화

        PlaceRooms();        // 방 배치
        Triangulate();       // 삼각분할
        CreateHallways();    // 복도 생성
        PathfindHallways();  // 경로 탐색 및 계단 배치
        OrganizeHallways();  // 복도 그룹화
        OrganizeStairs();    // 계단 그룹화 및 이름 변경

        // 노드 정보 직렬화
        serializedNodes = new List<Node>(nodes.Values);

        // 임시 부모 오브젝트 제거
        if (tempParent != null) {
            DestroyImmediate(tempParent);
        }

        SetRoomProperties();

        stopwatch.Stop();

        if (Application.isPlaying == false)
            Debug.Log($"던전 생성 완료: {stopwatch.ElapsedMilliseconds / 1000.0f}ms");
    }

    [ExecuteAlways]
    public void Clear() {
        Debug.Log("던전 데이터 초기화 시작");
        
        // 모든 자식 오브젝트 제거
        while (transform.childCount > 0) {
            DestroyImmediate(transform.GetChild(0).gameObject);
        }

        // 카테고리 카운터 초기화
        roomCounter = 0;

        // 딕셔너리 초기화
        stairGroups.Clear();
        pathGroups.Clear();
        positionToPathIndex.Clear();

        // 기존 데이터 초기화
        if (nodes != null) {
            nodes.Clear();
            Debug.Log("노드 데이터 초기화 완료");
        }
        if (grid != null) grid = null;
        if (rooms != null) rooms.Clear();
        if (delaunay != null) delaunay = null;
        if (selectedEdges != null) selectedEdges.Clear();
        if (connectedRooms != null) connectedRooms.Clear();
        if (pathLines != null) pathLines.Clear();

        Debug.Log("던전 데이터 초기화 완료");
    }

    // 카테고리 오브젝트 생성
    private void CreateCategoryObjects() {
        roomsCategory = new GameObject("Rooms");
        roomsCategory.transform.SetParent(transform);
        
        pathsCategory = new GameObject("Paths");
        pathsCategory.transform.SetParent(transform);

        tempParent = new GameObject("Temp");
        tempParent.transform.SetParent(transform);
    }

    // 3D 방 배치 메서드
    void PlaceRooms() {
        int createdRooms = 0;  // 실제 생성된 방의 개수

        for (int i = 0; i < tryMakeRoomCount && createdRooms < roomCount; i++) {
            // 랜덤한 위치 생성
            Vector3Int location = new Vector3Int(
                random.Next(0, size.x),
                random.Next(0, size.y),
                random.Next(0, size.z)
            );

            // 랜덤한 방 크기 생성
            Vector3Int roomSize = new Vector3Int(
                random.Next(roomMinSize.x, roomMaxSize.x + 1),
                random.Next(roomMinSize.y, roomMaxSize.y + 1),
                random.Next(roomMinSize.z, roomMaxSize.z + 1)
            );

            bool add = true;
            Room3D newRoom = new Room3D(location, roomSize);
            // 방 주변에 버퍼 영역 생성
            Room3D buffer = new Room3D(location + new Vector3Int(-2, -2, -2), 
                                 roomSize + new Vector3Int(4, 4, 4));

            // 다른 방들과의 겹침 검사
            foreach (var room in rooms) {
                if (Room3D.Intersect(room, buffer)) {
                    add = false;
                    break;
                }
            }

            // 던전 경계 검사
            if (newRoom.bounds.xMin < 0 || newRoom.bounds.xMax >= size.x
                || newRoom.bounds.yMin < 0 || newRoom.bounds.yMax >= size.y
                || newRoom.bounds.zMin < 0 || newRoom.bounds.zMax >= size.z) {
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

        // 원하는 수만큼 방을 생성하지 못한 경우 경고
        if (createdRooms < roomCount) {
            Debug.LogWarning($"요청한 방 개수({roomCount})보다 적은 수의 방({createdRooms})이 생성되었습니다.");
        }
    }

    /// <summary>
    /// 3D 들로네 삼각분할 수행 메서드
    /// </summary>
    void Triangulate() {
        List<Vertex> vertices = new List<Vertex>();

        // 각 방의 중심점을 정점으로 변환
        foreach (var room in rooms) {
            vertices.Add(new Vertex<Room3D>((Vector3)room.bounds.position + ((Vector3)room.bounds.size) / 2, room));
        }

        // 3D 들로네 삼각분할 수행
        delaunay = Delaunay3D.Triangulate(vertices);
    }

    /// <summary>
    /// 복도 생성을 위한 간선 선택 메서드
    /// </summary>
    void CreateHallways() {
        connectedRooms = new HashSet<Room3D>();
        selectedEdges = new HashSet<Prim.Edge>();
        List<Prim.Edge> edges = new List<Prim.Edge>();

        if (delaunay?.Edges == null || rooms == null || rooms.Count == 0) {
            Debug.LogError("들로네 삼각분할이 수행되지 않았거나 방이 없습니다.");
            return;
        }

        try {
            // 모든 방 쌍 사이의 간선 생성
            for (int i = 0; i < rooms.Count; i++) {
                for (int j = i + 1; j < rooms.Count; j++) {
                    if (rooms[i] != null && rooms[j] != null) {
                        var edge = new Prim.Edge(
                            new Vertex<Room3D>((Vector3)rooms[i].bounds.position + ((Vector3)rooms[i].bounds.size) / 2, rooms[i]),
                            new Vertex<Room3D>((Vector3)rooms[j].bounds.position + ((Vector3)rooms[j].bounds.size) / 2, rooms[j])
                        );
                        edges.Add(edge);
                    }
                }
            }

            // 첫 번째 방을 시작으로 최소 신장 트리 생성
            var mst = Prim.MinimumSpanningTree(edges, edges[0].U);
            
            if (mst != null) {
                foreach (var edge in mst) {
                    if (edge?.U is Vertex<Room3D> u && edge?.V is Vertex<Room3D> v) {
                        selectedEdges.Add(edge);
                        connectedRooms.Add(u.Item);
                        connectedRooms.Add(v.Item);
                    }
                }
            }

            // 연결되지 않은 방이 있는지 확인
            var unconnectedRooms = new HashSet<Room3D>(rooms);
            unconnectedRooms.ExceptWith(connectedRooms);

            if (unconnectedRooms.Count > 0) {
                Debug.LogWarning($"연결되지 않은 방 발견: {unconnectedRooms.Count}개");
                
                // 연결되지 않은 각 방에 대해
                foreach (var room in unconnectedRooms) {
                    // 이미 연결된 방들 중 가장 가까운 방 찾기
                    Room3D nearestRoom = null;
                    float minDistance = float.MaxValue;

                    foreach (var connectedRoom in connectedRooms) {
                        float distance = Vector3.Distance(
                            room.bounds.center,
                            connectedRoom.bounds.center
                        );
                        
                        if (distance < minDistance) {
                            minDistance = distance;
                            nearestRoom = connectedRoom;
                        }
                    }

                    if (nearestRoom != null) {
                        // 강제로 간선 추가
                        var edge = new Prim.Edge(
                            new Vertex<Room3D>((Vector3)room.bounds.position + ((Vector3)room.bounds.size) / 2, room),
                            new Vertex<Room3D>((Vector3)nearestRoom.bounds.position + ((Vector3)nearestRoom.bounds.size) / 2, nearestRoom)
                        );
                        selectedEdges.Add(edge);
                        connectedRooms.Add(room);
                    }
                }

                // 최종 확인
                unconnectedRooms = new HashSet<Room3D>(rooms);
                unconnectedRooms.ExceptWith(connectedRooms);
                
                if (unconnectedRooms.Count > 0) {
                    Debug.LogError($"여전히 연결되지 않은 방이 있습니다: {unconnectedRooms.Count}개");
                    foreach (var room in unconnectedRooms) {
                        Debug.LogError($"연결되지 않은 방 위치: {room.bounds.position}");
                    }
                }
            }

            // 모든 방이 연결된 후 순환 경로 추가
            if (edges.Count > 0) {
                // 5% 확률로 추가 간선 선택 (순환 경로 생성)
                var remainingEdges = new HashSet<Prim.Edge>(edges);
                remainingEdges.ExceptWith(selectedEdges);

                foreach (var edge in remainingEdges) {
                    if (random.NextDouble() < 0.01) {
                        selectedEdges.Add(edge);
                    }
                }
            }
        }
        catch (Exception e) {
            Debug.LogError($"복도 생성 중 치명적 오류 발생: {e.Message}");
        }
    }

    // A* 경로 탐색과 복도/계단 생성 메서드
    void PathfindHallways() {
        DungeonPathfinder3D aStar = new DungeonPathfinder3D(size);
        int currentPathIndex = 0;

        foreach (var edge in selectedEdges) {
            var startRoom = (edge.U as Vertex<Room3D>).Item;
            var endRoom = (edge.V as Vertex<Room3D>).Item;

            // 각 방의 가장 낮은 높이 찾기
            var startLowestY = startRoom.bounds.yMin;
            var endLowestY = endRoom.bounds.yMin;

            // 시작점과 도착점을 방의 가장자리까지 확장
            var startPos = GetRoomEdgePosition(startRoom, endRoom, true);
            var endPos = GetRoomEdgePosition(endRoom, startRoom, false);

            var path = aStar.FindPath(startPos, endPos, (DungeonPathfinder3D.Node a, DungeonPathfinder3D.Node b) => {
                var pathCost = new DungeonPathfinder3D.PathCost();
                var delta = b.Position - a.Position;

                // 계단 영역을 통과할 수 없도록 설정
                if (grid[b.Position] == CellType.Stairs) {
                    return pathCost;  // 계단은 통과 불가
                }

                if (delta.y == 0) {
                    // 평평한 복도의 경우
                    pathCost.cost = Vector3Int.Distance(b.Position, endPos);    // 목표까지의 거리를 휴리스틱으로 사용

                    // 셀 타입에 따른 이동 비용 설정
                    if (grid[b.Position] == CellType.Room) {
                        pathCost.cost += 5;  // 방을 통과하는 비용
                    } else if (grid[b.Position] == CellType.None) {
                        pathCost.cost += 1;  // 빈 공간을 통과하는 비용
                    }

                    pathCost.traversable = true;
                } else {
                    // 계단 설치의 경우
                    // 시작점과 도착점이 비어있거나 복도인 경우만 계단 설치 가능
                    if ((grid[a.Position] != CellType.None && grid[a.Position] != CellType.Hallway)
                        || (grid[b.Position] != CellType.None && grid[b.Position] != CellType.Hallway)) 
                        return pathCost;

                    // 계단 설치 비용 계산
                    pathCost.cost = 100 + Vector3Int.Distance(b.Position, endPos);    // 기본 비용 + 휴리스틱

                    // 계단의 방향 벡터 계산
                    int xDir = Mathf.Clamp(delta.x, -1, 1);
                    int zDir = Mathf.Clamp(delta.z, -1, 1);
                    Vector3Int verticalOffset = new Vector3Int(0, delta.y, 0);    // 수직 이동량
                    Vector3Int horizontalOffset = new Vector3Int(xDir, 0, zDir);   // 수평 이동량

                    // 계단 설치 공간이 그리드 범위 내인지 확인
                    if (!grid.InBounds(a.Position + verticalOffset)
                        || !grid.InBounds(a.Position + horizontalOffset)
                        || !grid.InBounds(a.Position + verticalOffset + horizontalOffset)) {
                        return pathCost;
                    }

                    // 계단 설치 공간이 비어있는지 확인
                    // 현재 위치와 목표 위치도 포함하여 확인
                    if (grid[a.Position] != CellType.None
                        || grid[b.Position] != CellType.None
                        || grid[a.Position + horizontalOffset] != CellType.None
                        || grid[a.Position + horizontalOffset * 2] != CellType.None
                        || grid[a.Position + verticalOffset] != CellType.None
                        || grid[a.Position + verticalOffset + horizontalOffset] != CellType.None
                        || grid[a.Position + verticalOffset + horizontalOffset * 2] != CellType.None) {
                        return pathCost;
                    }

                    // 계단 설치 후의 공간도 확인
                    if (grid.InBounds(b.Position + horizontalOffset)
                        && grid[b.Position + horizontalOffset] != CellType.None) {
                        return pathCost;
                    }

                    pathCost.traversable = true;
                    pathCost.isStairs = true;
                }

                return pathCost;
            });

            // 경로가 존재하는 경우에만 처리
            if (path != null && path.Count > 0) {
                // 목표 방의 외곽에 처음 닿는 지점을 찾아 경로를 자릅니다
                int endIndex = path.Count;
                for (int i = 0; i < path.Count; i++) {
                    var pos = path[i];
                    // 현재 위치가 목표 방 안에 있는지 확인
                    if (endRoom.bounds.Contains(pos)) {
                        // 현재 위치(외곽)까지 포함
                        endIndex = i + 1;
                        break;
                    }
                }

                // 경로를 외곽 지점까지 사용
                path = path.GetRange(0, endIndex);
                
                // 경로 저장
                pathLines.Add(new List<Vector3Int>(path));

                // 먼저 모든 위치를 복도로 표시
                foreach (var pos in path) {
                    if (grid[pos] == CellType.None) {
                        grid[pos] = CellType.Hallway;
                    }
                }

                // 그 다음 계단 설치
                for (int i = 1; i < path.Count; i++) {
                    var prev = path[i - 1];
                    var current = path[i];
                    var delta = current - prev;

                    // 높이 차이가 있는 경우 계단 설치
                    if (delta.y != 0) {
                        int xDir = Mathf.Clamp(delta.x, -1, 1);
                        int zDir = Mathf.Clamp(delta.z, -1, 1);
                        Vector3Int verticalOffset = new Vector3Int(0, delta.y, 0);
                        Vector3Int horizontalOffset = new Vector3Int(xDir, 0, zDir);
                        
                        // 계단 영역 설정
                        Vector3Int[] stairPositions = {
                            prev + horizontalOffset,
                            prev + horizontalOffset * 2,
                            prev + verticalOffset + horizontalOffset,
                            prev + verticalOffset + horizontalOffset * 2
                        };

                        // 계단 영역을 그리드에 표시
                        foreach (var pos in stairPositions) {
                            grid[pos] = CellType.Stairs;
                            positionToPathIndex[pos] = currentPathIndex;
                        }

                        // 계단 시각화 오브젝트 생성
                        PlaceStairs(stairPositions);
                    }
                }

                // 마지막으로 노드 정보 생성 및 방향 설정
                for (int i = 0; i < path.Count; i++) {
                    var current = path[i];
                    positionToPathIndex[current] = currentPathIndex;

                    // 노드 정보 가져오기 또는 생성
                    if (!nodes.TryGetValue(current, out Node node)) {
                        node = new Node(current);
                        nodes[current] = node;
                    }

                    // 이전 노드와의 방향 설정
                    if (i > 0) {
                        var prev = path[i - 1];
                        var direction = GetDirection(prev, current);
                        var oppositeDirection = GetOppositeDirection(direction);
                        if (!node.OuterDirections.Contains(oppositeDirection)) {
                            node.OuterDirections.Add(oppositeDirection);
                        }
                    }

                    // 다음 노드와의 방향 설정
                    if (i < path.Count - 1) {
                        var next = path[i + 1];
                        var direction = GetDirection(current, next);
                        if (!node.OuterDirections.Contains(direction)) {
                            node.OuterDirections.Add(direction);
                        }
                    }
                }

                // 복도 시각화
                foreach (var pos in path) {
                    if (grid[pos] == CellType.Hallway) {
                        if (nodes.TryGetValue(pos, out Node node)) {
                            PlaceHallway(pos, node.OuterDirections);
                        }
                    }
                }

                currentPathIndex++;
            }
        }

        // 모든 방에 대해 셀 분석 및 시각화 수행
        foreach (var room in rooms) {
            AnalyzeRoomCells(room);
            VisualizeRoom(room);
        }
    }

    // 방의 가장자리 위치를 계산하는 메서드
    private Vector3Int GetRoomEdgePosition(Room3D room, Room3D targetRoom, bool isStart) {
        Vector3Int roomCenter = new Vector3Int(
            (int)room.bounds.center.x,
            room.bounds.yMin,
            (int)room.bounds.center.z
        );

        Vector3Int targetCenter = new Vector3Int(
            (int)targetRoom.bounds.center.x,
            targetRoom.bounds.yMin,
            (int)targetRoom.bounds.center.z
        );

        Vector3Int edgePos;
        
        if (isStart) {
            // 시작점: 현재 방에서 목표 방으로 향하는 방향으로 가장자리 찾기
            Vector3 direction = ((Vector3)(targetCenter - roomCenter)).normalized;
            
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.z)) {
                // X 방향이 주된 이동 방향
                edgePos = new Vector3Int(
                    direction.x > 0 ? room.bounds.xMax - 1 : room.bounds.xMin,
                    room.bounds.yMin,
                    roomCenter.z
                );
            } else {
                // Z 방향이 주된 이동 방향
                edgePos = new Vector3Int(
                    roomCenter.x,
                    room.bounds.yMin,
                    direction.z > 0 ? room.bounds.zMax - 1 : room.bounds.zMin
                );
            }
        } else {
            // 끝점: 상대 방에서 가장 가까운 현재 방의 가장자리 찾기
            Vector3Int closestEdge = Vector3Int.zero;
            float minDistance = float.MaxValue;

            // 상대 방의 중심에서 가장 가까운 현재 방의 가장자리 찾기
            // X축 가장자리 검사
            int[] xPoints = new int[] { room.bounds.xMin, room.bounds.xMax - 1 };
            foreach (int x in xPoints) {
                for (int z = room.bounds.zMin; z < room.bounds.zMax; z++) {
                    Vector3Int pos = new Vector3Int(x, room.bounds.yMin, z);
                    float dist = Vector3.Distance(pos, targetCenter);
                    if (dist < minDistance) {
                        minDistance = dist;
                        closestEdge = pos;
                    }
                }
            }

            // Z축 가장자리 검사
            int[] zPoints = new int[] { room.bounds.zMin, room.bounds.zMax - 1 };
            foreach (int z in zPoints) {
                for (int x = room.bounds.xMin; x < room.bounds.xMax; x++) {
                    Vector3Int pos = new Vector3Int(x, room.bounds.yMin, z);
                    float dist = Vector3.Distance(pos, targetCenter);
                    if (dist < minDistance) {
                        minDistance = dist;
                        closestEdge = pos;
                    }
                }
            }

            edgePos = closestEdge;
        }

        return edgePos;
    }

    // 큐브 오브젝트를 생성하여 배치하는 메서드
    void PlaceCube(Vector3Int location, Vector3Int size, Material material) {
        // 월드 좌표 계산
        Vector3 worldPos = new Vector3(
            location.x * UnitPerGrid,
            location.y * UnitPerGrid,
            location.z * UnitPerGrid
        );

        GameObject go = Instantiate(cubePrefab, worldPos, Quaternion.identity);
        go.transform.SetParent(transform);
        go.GetComponent<Transform>().localScale = new Vector3(
            size.x * UnitPerGrid,
            size.y * UnitPerGrid,
            size.z * UnitPerGrid
        );
        go.GetComponent<MeshRenderer>().material = material;
    }

    // 방을 시각화하는 메서드 (빨간색)
    void PlaceRoom(Vector3Int location, Vector3Int size) {
        // 월드 좌표 계산
        Vector3 worldPos = new Vector3(
            location.x * UnitPerGrid,
            location.y * UnitPerGrid,
            location.z * UnitPerGrid
        );

        // 스케일에 따른 오프셋 계산 (크기에서 1을 뺀 값으로 계산)
        Vector3 offset = new Vector3(
            (size.x - 1) * 0.5f * UnitPerGrid,
            (size.y - 1) * 0.5f * UnitPerGrid,
            (size.z - 1) * 0.5f * UnitPerGrid
        );

        // 방 오브젝트 생성
        GameObject roomObj = new GameObject($"Room-{++roomCounter}-[{location.x},{location.y},{location.z}]");
        roomObj.transform.position = worldPos + offset;
        roomObj.transform.SetParent(roomsCategory.transform);

        // RoomCube 생성
        GameObject roomCube = Instantiate(cubePrefab, worldPos, Quaternion.identity);
        roomCube.name = "RoomCube";
        roomCube.transform.position = worldPos + offset;
        roomCube.transform.SetParent(roomObj.transform);
        
        roomCube.transform.localScale = new Vector3(
            size.x * UnitPerGrid,
            size.y * UnitPerGrid,
            size.z * UnitPerGrid
        );
        roomCube.GetComponent<MeshRenderer>().material = normalRoomMaterial;

        // Meshes 하위 오브젝트 생성
        GameObject meshesObj = new GameObject("Meshes");
        meshesObj.transform.SetParent(roomObj.transform);
        
        // RoomVisualizer 컴포넌트 추가
        var roomVisualizer = roomObj.AddComponent<RoomVisualizer>();
        roomVisualizer.RoomType = RoomType.Normal; // 기본값 설정

        // 방의 셀 속성 분석
        var room = rooms[rooms.Count - 1]; // 가장 최근에 추가된 방
        room.VisualizationObject = roomObj;
    }

    // 복도를 시각화하는 메서드 (파란색)
    void PlaceHallway(Vector3Int location, List<PathDirection> outerDirections) {
        // 이미 해당 위치에 시각화 오브젝트가 있는지 확인
        Transform existingObject = tempParent.transform.Find($"Hallway-[{location.x},{location.y},{location.z}]");
        if (existingObject != null) {
            // 기존 오브젝트의 방향 정보 업데이트
            UpdateHallwayDirections(existingObject.gameObject, outerDirections);
            return;
        }

        // 월드 좌표 계산
        Vector3 worldPos = new Vector3(
            location.x * UnitPerGrid,
            location.y * UnitPerGrid,
            location.z * UnitPerGrid
        );

        GameObject go = Instantiate(cubePrefab, worldPos, Quaternion.identity);
        go.transform.SetParent(tempParent.transform);
        go.GetComponent<Transform>().localScale = Vector3.one * UnitPerGrid;
        go.GetComponent<MeshRenderer>().material = hallwayMaterial;
        go.name = $"Hallway-[{location.x},{location.y},{location.z}]";

        // 방향에 따른 회전 설정
        SetHallwayRotation(go, outerDirections);
    }

    // 복도 오브젝트의 방향 정보 업데이트
    private void UpdateHallwayDirections(GameObject hallway, List<PathDirection> outerDirections) {
        SetHallwayRotation(hallway, outerDirections);
    }

    // 복도 오브젝트의 회전 설정
    private void SetHallwayRotation(GameObject hallway, List<PathDirection> outerDirections) {
        // 방향에 따른 회전 로직 구현
        // 예: X축과 Z축 방향이 모두 열려있는 경우 45도 회전 등
        // 실제 구현은 시각화 요구사항에 따라 달라질 수 있음
    }

    // 계단을 시각화하는 메서드 (초록색)
    void PlaceStairs(Vector3Int[] stairPositions) {
        if (stairPositions.Length != 4) {
            Debug.LogError("계단 그룹은 정확히 4개의 위치가 필요합니다.");
            return;
        }

        // 계단 그룹 생성
        GameObject stairGroup = new GameObject($"Stair-[{stairPositions[0].x},{stairPositions[0].y},{stairPositions[0].z}]-[{stairPositions[3].x},{stairPositions[3].y},{stairPositions[3].z}]");
        stairGroup.transform.SetParent(tempParent.transform);

        // 각 계단 오브젝트 생성
        for (int i = 0; i < 4; i++) {
            // 월드 좌표 계산
            Vector3 worldPos = new Vector3(
                stairPositions[i].x * UnitPerGrid,
                stairPositions[i].y * UnitPerGrid,
                stairPositions[i].z * UnitPerGrid
            );

            GameObject go = Instantiate(cubePrefab, worldPos, Quaternion.identity);
            go.transform.SetParent(stairGroup.transform);
            go.GetComponent<Transform>().localScale = Vector3.one * UnitPerGrid;
            go.GetComponent<MeshRenderer>().material = stairMaterial;
            go.name = $"{(char)('A' + i)}[{stairPositions[i].x},{stairPositions[i].y},{stairPositions[i].z}]";
        }
    }

    // 복도 그룹 생성 및 이름 변경
    private void OrganizeHallways() {
        // 모든 복도 오브젝트를 찾아서 그룹화
        List<Transform> children = new List<Transform>();
        foreach (Transform child in tempParent.transform) {
            children.Add(child);
        }

        foreach (Transform child in children) {
            if (child.name.StartsWith("Hallway-")) {
                // 좌표 추출
                string coordStr = child.name.Split('[')[1].Split(']')[0];
                string[] coords = coordStr.Split(',');
                Vector3Int location = new Vector3Int(
                    int.Parse(coords[0]),
                    int.Parse(coords[1]),
                    int.Parse(coords[2])
                );

                // 해당 위치가 속한 경로 인덱스 찾기
                if (!positionToPathIndex.TryGetValue(location, out int pathIndex)) {
                    continue;
                }

                // 복도 그룹 생성 또는 가져오기
                if (!pathGroups.ContainsKey(pathIndex)) {
                    GameObject pathGroup = new GameObject($"Path-[{pathIndex + 1}]");
                    pathGroup.transform.SetParent(pathsCategory.transform);
                    pathGroups[pathIndex] = pathGroup;
                }

                // 복도 오브젝트를 그룹으로 이동
                child.SetParent(pathGroups[pathIndex].transform);
            }
        }
    }

    // 계단 그룹 생성 및 이름 변경
    private void OrganizeStairs() {
        if (pathsCategory == null) {
            Debug.LogError("Paths 카테고리가 초기화되지 않았습니다.");
            return;
        }

        // 모든 계단 그룹을 찾아서 처리
        List<Transform> children = new List<Transform>();
        foreach (Transform child in tempParent.transform) {
            children.Add(child);
        }

        foreach (Transform child in children) {
            if (child.name.StartsWith("Stair-")) {
                // 계단 그룹의 첫 번째 계단에서 좌표 추출
                Transform firstStair = child.GetChild(0);
                string coordStr = firstStair.name.Split('[')[1].Split(']')[0];
                string[] coords = coordStr.Split(',');
                Vector3Int location = new Vector3Int(
                    int.Parse(coords[0]),
                    int.Parse(coords[1]),
                    int.Parse(coords[2])
                );

                // 해당 위치가 속한 경로 인덱스 찾기
                if (!positionToPathIndex.TryGetValue(location, out int pathIndex)) {
                    Debug.LogWarning($"계단 위치 {location}에 대한 경로 인덱스를 찾을 수 없습니다.");
                    continue;
                }

                // 해당 경로의 부모 오브젝트 찾기
                string pathName = $"Path-[{pathIndex + 1}]";
                Transform pathParent = pathsCategory.transform.Find(pathName);
                
                if (pathParent == null) {
                    continue;
                }

                // 계단 그룹을 해당 경로 아래로 이동
                child.SetParent(pathParent);
            }
        }

        // Temp 폴더에 남아있는 오브젝트들 제거
        while (tempParent.transform.childCount > 0) {
            DestroyImmediate(tempParent.transform.GetChild(0).gameObject);
        }
    }

    private void OnDrawGizmosSelected() {
        if (selectedEdges == null) {
            return;
        }

        // MST 간선 시각화
        if (showMSTEdges) {
            Gizmos.color = mstColor;
            foreach (var edge in selectedEdges) {
                if (edge?.U is Vertex<Room3D> u && edge?.V is Vertex<Room3D> v) {
                    var startRoom = u.Item;
                    var endRoom = v.Item;

                    // 방의 중심점 계산 (UnitPerGrid 적용)
                    Vector3 start = new Vector3(
                        startRoom.bounds.center.x * UnitPerGrid,
                        startRoom.bounds.center.y * UnitPerGrid,
                        startRoom.bounds.center.z * UnitPerGrid
                    );
                    Vector3 end = new Vector3(
                        endRoom.bounds.center.x * UnitPerGrid,
                        endRoom.bounds.center.y * UnitPerGrid,
                        endRoom.bounds.center.z * UnitPerGrid
                    );

                    // 연결선 그리기
                    Gizmos.DrawLine(start, end);
                    
                    // 방향 표시를 위한 구체 추가
                    Gizmos.DrawSphere(start, 0.2f * UnitPerGrid);
                    Gizmos.DrawSphere(end, 0.2f * UnitPerGrid);
                }
            }
        }
    }

    private void OnDrawGizmos() {
        if (nodes == null) return;

        // 경로 생성 과정 시각화
        if (pathLines != null) {
            for (int i = 0; i < pathLines.Count; i++) {
                var path = pathLines[i];
                for (int j = 1; j < path.Count; j++) {
                    // UnitPerGrid 적용
                    Vector3 start = new Vector3(
                        path[j-1].x * UnitPerGrid,
                        path[j-1].y * UnitPerGrid,
                        path[j-1].z * UnitPerGrid
                    );
                    Vector3 end = new Vector3(
                        path[j].x * UnitPerGrid,
                        path[j].y * UnitPerGrid,
                        path[j].z * UnitPerGrid
                    );

                    // 경로의 시작점과 끝점을 다른 색상으로 표시
                    if (j == 1) {
                        Gizmos.color = Color.green;
                        Gizmos.DrawSphere(start, 0.2f * UnitPerGrid);
                    }
                    if (j == path.Count - 1) {
                        Gizmos.color = Color.red;
                        Gizmos.DrawSphere(end, 0.2f * UnitPerGrid);
                    }

                    // 경로의 진행 방향을 화살표로 표시
                    Gizmos.color = Color.blue;
                    Gizmos.DrawLine(start, end);
                }
            }
        }

        // 노드의 열린 방향 시각화
        foreach (var node in nodes.Values) {
            if (node.OuterDirections.Count == 0) continue;

            // UnitPerGrid 적용
            Vector3 center = new Vector3(
                node.Position.x * UnitPerGrid,
                node.Position.y * UnitPerGrid,
                node.Position.z * UnitPerGrid
            );
            float arrowLength = 0.3f * UnitPerGrid;
            float arrowHeadLength = 0.1f * UnitPerGrid;
            float arrowHeadAngle = 20.0f;

            foreach (var direction in node.OuterDirections) {
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
                    case PathDirection.ZPlus:
                        dir = Vector3.forward;
                        color = Color.white;
                        break;
                    case PathDirection.ZMinus:
                        dir = Vector3.back;
                        color = Color.black;
                        break;
                }

                // 인접 셀 확인
                Vector3Int neighborPos = node.Position + new Vector3Int(
                    direction == PathDirection.XPlus ? 1 : direction == PathDirection.XMinus ? -1 : 0,
                    0,
                    direction == PathDirection.ZPlus ? 1 : direction == PathDirection.ZMinus ? -1 : 0
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

    private void SetRoomProperties() {
        // 시작방 찾기 (원점에서 가장 가까운 방)
        startRoom = FindClosestRoomToOrigin();
        startRoom.Properties.RoomType = RoomType.Start;
        startRoom.Properties.HasEnemies = random.Next(0, 100) < generationOptions.EnemySpawnChance;
        startRoom.Properties.HasItemChest = random.Next(0, 100) < generationOptions.ItemChestSpawnChance;

        // 출구방 찾기 (시작방에서 가장 먼 방)
        exitRoom = FindFarthestRoomFromStart();
        exitRoom.Properties.RoomType = RoomType.Exit;
        exitRoom.Properties.HasEnemies = random.Next(0, 100) < generationOptions.EnemySpawnChance;
        exitRoom.Properties.HasItemChest = random.Next(0, 100) < generationOptions.ItemChestSpawnChance;

        // 보스 설정
        if (generationOptions.SpawnBoss) {
            exitRoom.Properties.HasBoss = true;
            exitRoom.Properties.HasEnemies = true; // 보스 방은 무조건 적 출현
        }

        // 상점 방 설정
        if (generationOptions.SpawnMerchant) {
            int merchantRoomIndex = random.Next(0, rooms.Count);
            merchantRoom = rooms[merchantRoomIndex];
            merchantRoom.Properties.HasMerchant = true;
        }

        // 모든 방에 대해 속성 설정
        foreach (var room in rooms) {
            if (room == startRoom || room == exitRoom) continue;

            // 적 출현 확률
            if (random.Next(0, 100) < generationOptions.EnemySpawnChance) {
                room.Properties.HasEnemies = true;
            }

            // 아이템 상자 출현 확률
            if (random.Next(0, 100) < generationOptions.ItemChestSpawnChance) {
                room.Properties.HasItemChest = true;
            }
        }

        // 시각화 오브젝트의 RoomProperties 업데이트
        UpdateRoomVisualization();
    }

    private void UpdateRoomVisualization() {
        foreach (Transform child in roomsCategory.transform) {
            // 기본 이름 구성
            string baseName = child.name;
            if (child.name.StartsWith("Room-")) {
                var roomVisualizer = child.GetComponent<RoomVisualizer>();
                if (roomVisualizer != null) {
                    // 방 번호 추출
                    string roomNumberStr = child.name.Split('-')[1].Split('[')[0];
                    if (int.TryParse(roomNumberStr, out int roomIndex) && roomIndex <= rooms.Count) {
                        var room = rooms[roomIndex - 1];
                        roomVisualizer.RoomType = room.Properties.RoomType;
                        roomVisualizer.HasEnemies = room.Properties.HasEnemies;
                        roomVisualizer.HasItemChest = room.Properties.HasItemChest;
                        roomVisualizer.HasMerchant = room.Properties.HasMerchant;
                        roomVisualizer.HasBoss = room.Properties.HasBoss;

                        // RoomCube의 머티리얼 변경
                        Transform roomCube = child.Find("RoomCube");
                        if (roomCube != null) {
                            if (room.Properties.RoomType == RoomType.Start) {
                                baseName += "-Start";
                                roomCube.GetComponent<MeshRenderer>().material = startRoomMaterial;
                            } else if (room.Properties.RoomType == RoomType.Exit) {
                                baseName += "-End";
                                roomCube.GetComponent<MeshRenderer>().material = endRoomMaterial;
                            }
                            if (room.Properties.HasMerchant) {
                                baseName += "-Shop";
                                roomCube.GetComponent<MeshRenderer>().material = merchantRoomMaterial;
                            }
                        }
                        
                        child.name = baseName;
                    }
                }
            }
        }
    }

    private Room3D FindClosestRoomToOrigin() {
        Room3D closestRoom = null;
        float minDistance = float.MaxValue;
        Vector3 origin = Vector3.zero;

        foreach (var room in rooms) {
            float distance = Vector3.Distance(room.bounds.center, origin);
            if (distance < minDistance) {
                minDistance = distance;
                closestRoom = room;
            }
        }

        return closestRoom;
    }

    private Room3D FindFarthestRoomFromStart() {
        Room3D farthestRoom = null;
        float maxDistance = 0;

        foreach (var room in rooms) {
            if (room == startRoom) continue;

            float distance = Vector3.Distance(room.bounds.center, startRoom.bounds.center);
            if (distance > maxDistance) {
                maxDistance = distance;
                farthestRoom = room;
            }
        }

        return farthestRoom;
    }

    private Vector3Int GetDirectionOffset(PathDirection direction) {
        switch (direction) {
            case PathDirection.XPlus: return new Vector3Int(1, 0, 0);
            case PathDirection.XMinus: return new Vector3Int(-1, 0, 0);
            case PathDirection.YPlus: return new Vector3Int(0, 1, 0);
            case PathDirection.YMinus: return new Vector3Int(0, -1, 0);
            case PathDirection.ZPlus: return new Vector3Int(0, 0, 1);
            case PathDirection.ZMinus: return new Vector3Int(0, 0, -1);
            default: return Vector3Int.zero;
        }
    }

    private void AnalyzeRoomCells(Room3D room) {
        foreach (var pos in room.bounds.allPositionsWithin) {
            var cellProps = new CellProperties(pos);

            // 각 방향에 대해 인접 셀 확인
            foreach (PathDirection direction in Enum.GetValues(typeof(PathDirection))) {
                if (direction == PathDirection.None) continue;

                var neighborPos = pos + GetDirectionOffset(direction);
                
                // 그리드 범위를 벗어나거나 방 내부가 아닌 경우
                if (!grid.InBounds(neighborPos) || !room.bounds.Contains(neighborPos)) {
                    // 해당 방향이 열려있는 방향으로 추가
                    cellProps.OuterDirections.Add(direction);
                }
                    
                // 복도와 연결된 경우
                if (grid.InBounds(neighborPos) && grid[neighborPos] == CellType.Hallway) {
                    // 해당 위치의 노드 정보 가져오기
                    if (nodes.TryGetValue(neighborPos, out Node neighborNode)) {
                        // 인접 복도의 방향이 현재 방향의 역방향인지 확인
                        PathDirection oppositeDirection = GetOppositeDirection(direction);
                        if (neighborNode.OuterDirections.Contains(oppositeDirection)) {
                            cellProps.IsEntrance = true;
                            cellProps.EntranceDirections.Add(direction);
                            Debug.Log($"출입구 추가: 위치 {pos}, 방향 {direction}");
                        }
                    }
                }
            }

            room.CellProperties[pos] = cellProps;
        }
    }

    private void VisualizeRoom(Room3D room) {
        if (room.VisualizationObject == null) {
            Debug.LogError("Room visualization object is null");
            return;
        }

        // Meshes 오브젝트 찾기
        Transform meshesObj = room.VisualizationObject.transform.Find("Meshes");
        if (meshesObj == null) {
            Debug.LogError("Meshes object not found");
            return;
        }

        foreach (var cellProps in room.CellProperties.Values) {
            // 셀의 월드 좌표 계산
            Vector3 worldPos = new Vector3(
                cellProps.Position.x * UnitPerGrid,
                cellProps.Position.y * UnitPerGrid,
                cellProps.Position.z * UnitPerGrid
            );

            // 셀 오브젝트 생성
            GameObject cellObj = new GameObject($"Cell [{cellProps.Position.x},{cellProps.Position.y},{cellProps.Position.z}]");
            cellObj.transform.SetParent(meshesObj);
            cellObj.transform.position = worldPos;

            // 각 방향에 대해 처리
            foreach (var direction in cellProps.OuterDirections) {
                GameObject prefabToInstantiate = null;
                Vector3 rotation = Vector3.zero;
                Vector3 position = worldPos;

                // 방향에 따른 프리팹과 회전 설정
                switch (direction) {
                    case PathDirection.YPlus:  // 천정
                        prefabToInstantiate = ceilingPrefab;
                        break;
                    case PathDirection.YMinus:  // 바닥
                        prefabToInstantiate = floorPrefab;
                        break;
                    case PathDirection.XPlus:  // 오른쪽 벽
                        prefabToInstantiate = cellProps.EntranceDirections.Contains(direction) ? entrancePrefab : wallPrefab;
                        rotation.y = 270f;
                        break;
                    case PathDirection.XMinus:  // 왼쪽 벽
                        prefabToInstantiate = cellProps.EntranceDirections.Contains(direction) ? entrancePrefab : wallPrefab;
                        rotation.y = 90f;
                        break;
                    case PathDirection.ZPlus:  // 앞쪽 벽
                        prefabToInstantiate = cellProps.EntranceDirections.Contains(direction) ? entrancePrefab : wallPrefab;
                        rotation.y = 180f;
                        break;
                    case PathDirection.ZMinus:  // 뒤쪽 벽
                        prefabToInstantiate = cellProps.EntranceDirections.Contains(direction) ? entrancePrefab : wallPrefab;
                        rotation.y = 0f;
                        break;
                }

                if (prefabToInstantiate != null) {
                    GameObject obj = Instantiate(prefabToInstantiate);
                    obj.transform.position = position;
                    obj.transform.rotation = Quaternion.Euler(rotation);
                    obj.transform.SetParent(cellObj.transform);
                }
            }
        }
    }
}
