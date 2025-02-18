using System;
using System.Collections.Generic;
using UnityEngine;
using BlueRaja;

/// <summary>
/// 3D 던전 경로 탐색기 클래스
/// 계단 배치를 위한 수정된 A* 알고리즘 구현
/// </summary>
public class DungeonPathfinder3D {
    // 노드 클래스 정의 - 경로 탐색에 사용되는 기본 단위
    public class Node {
        public Vector3Int Position { get; private set; }    // 노드의 3D 위치
        public Node Previous { get; set; }                  // 이전 노드 참조
        public HashSet<Vector3Int> PreviousSet { get; private set; }  // 이전 위치들의 집합 (계단 생성 시 사용)
        public float Cost { get; set; }                     // 시작점에서 현재까지의 누적 비용

        // 노드 생성자 - 위치 초기화 및 이전 위치 집합 생성
        public Node(Vector3Int position) {
            Position = position;
            PreviousSet = new HashSet<Vector3Int>();
        }
    }

    // 경로 비용 구조체 - 이동 가능 여부와 비용을 포함
    public struct PathCost {
        public bool traversable;    // 해당 위치로 이동 가능 여부
        public float cost;          // 이동에 필요한 비용
        public bool isStairs;       // 계단 설치 가능 여부
    }

    // 이웃 노드들의 상대적 위치 정의
    // 기본 이동(동서남북)과 계단 이동(상하) 모두 포함
    static readonly Vector3Int[] neighbors = {
        // 기본 이동 방향
        new Vector3Int(1, 0, 0),   // 동
        new Vector3Int(-1, 0, 0),  // 서
        new Vector3Int(0, 0, 1),   // 북
        new Vector3Int(0, 0, -1),  // 남

        // 위로 가는 계단
        new Vector3Int(3, 1, 0),   // 동쪽 계단
        new Vector3Int(-3, 1, 0),  // 서쪽 계단
        new Vector3Int(0, 1, 3),   // 북쪽 계단
        new Vector3Int(0, 1, -3),  // 남쪽 계단

        // 아래로 가는 계단
        new Vector3Int(3, -1, 0),  // 동쪽 아래 계단
        new Vector3Int(-3, -1, 0), // 서쪽 아래 계단
        new Vector3Int(0, -1, 3),  // 북쪽 아래 계단
        new Vector3Int(0, -1, -3), // 남쪽 아래 계단
    };

    // 경로 탐색에 필요한 데이터 구조들
    private Grid3D<Node> grid;                              // 3D 공간의 모든 노드를 저장하는 그리드
    private SimplePriorityQueue<Node, float> queue;         // A* 알고리즘용 우선순위 큐
    private HashSet<Node> closed;                           // 이미 방문한 노드들의 집합
    private Stack<Vector3Int> stack;                        // 경로 재구성을 위한 스택

    /// <summary>
    /// 경로 탐색기 생성자
    /// </summary>
    /// <param name="size">던전의 3차원 크기</param>
    public DungeonPathfinder3D(Vector3Int size) {
        grid = new Grid3D<Node>(size, Vector3Int.zero);
        queue = new SimplePriorityQueue<Node, float>();
        closed = new HashSet<Node>();
        stack = new Stack<Vector3Int>();

        // 모든 위치에 대해 노드 초기화
        for (int z = 0; z < size.z; z++) {
            for (int y = 0; y < size.y; y++) {
                for (int x = 0; x < size.x; x++) {
                    var pos = new Vector3Int(x, y, z);
                    grid[pos] = new Node(pos);
                }
            }
        }
    }

    /// <summary>
    /// 모든 노드의 상태를 초기화
    /// </summary>
    void ResetNodes() {
        queue.Clear();
        closed.Clear();
        
        for (int z = 0; z < grid.Size.z; z++) {
            for (int y = 0; y < grid.Size.y; y++) {
                for (int x = 0; x < grid.Size.x; x++) {
                    var node = grid[x, y, z];
                    node.Cost = float.MaxValue;
                    node.Previous = null;
                    node.PreviousSet.Clear();
                }
            }
        }
    }

    /// <summary>
    /// 시작점에서 목표점까지의 경로를 찾는 메서드
    /// </summary>
    /// <param name="start">시작 위치</param>
    /// <param name="end">목표 위치</param>
    /// <param name="costFunction">노드 간 이동 비용을 계산하는 함수</param>
    /// <returns>경로를 이루는 위치들의 리스트</returns>
    public List<Vector3Int> FindPath(Vector3Int start, Vector3Int end, Func<Node, Node, PathCost> costFunction) {
        ResetNodes();  // 노드 상태 초기화
        
        Node startNode = grid[start];
        startNode.Cost = 0;
        queue.Enqueue(startNode, 0);

        while (queue.Count > 0) {
            Node node = queue.Dequeue();

            // 목표 도달 시 경로 반환
            if (node.Position == end) {
                stack.Clear();
                
                // 경로 재구성
                Node current = node;
                while (current != null) {
                    stack.Push(current.Position);
                    current = current.Previous;
                }
                
                return new List<Vector3Int>(stack);
            }

            foreach (var offset in neighbors) {
                // 이웃 노드가 그리드 범위를 벗어나면 건너뛰기
                if (!grid.InBounds(node.Position + offset)) continue;
                var neighbor = grid[node.Position + offset];
                // 이미 방문한 노드면 건너뛰기
                if (closed.Contains(neighbor)) continue;

                // 이전에 방문한 위치면 순환 경로 방지를 위해 건너뛰기
                if (node.PreviousSet.Contains(neighbor.Position)) {
                    continue;
                }

                // 이웃 노드로의 이동 비용 계산
                var pathCost = costFunction(node, neighbor);
                // 이동 불가능한 위치면 건너뛰기
                if (!pathCost.traversable) continue;

                // 계단을 설치하는 경우
                if (pathCost.isStairs) {
                    // 계단의 방향 벡터 계산
                    int xDir = Mathf.Clamp(offset.x, -1, 1);
                    int zDir = Mathf.Clamp(offset.z, -1, 1);
                    Vector3Int verticalOffset = new Vector3Int(0, offset.y, 0);    // 수직 이동량
                    Vector3Int horizontalOffset = new Vector3Int(xDir, 0, zDir);   // 수평 이동량

                    // 계단 설치에 필요한 공간이 이미 사용 중이면 건너뛰기
                    if (node.PreviousSet.Contains(node.Position + horizontalOffset)
                        || node.PreviousSet.Contains(node.Position + horizontalOffset * 2)
                        || node.PreviousSet.Contains(node.Position + verticalOffset + horizontalOffset)
                        || node.PreviousSet.Contains(node.Position + verticalOffset + horizontalOffset * 2)) {
                        continue;
                    }
                }

                // 새로운 경로 비용 계산
                float newCost = node.Cost + pathCost.cost;

                // 더 낮은 비용의 경로를 발견한 경우
                if (newCost < neighbor.Cost) {
                    neighbor.Previous = node;
                    neighbor.Cost = newCost;

                    // 우선순위 큐 업데이트
                    if (queue.TryGetPriority(node, out float existingPriority)) {
                        queue.UpdatePriority(node, newCost);
                    } else {
                        queue.Enqueue(neighbor, neighbor.Cost);
                    }

                    // 이전 방문 위치 집합 업데이트
                    neighbor.PreviousSet.Clear();
                    neighbor.PreviousSet.UnionWith(node.PreviousSet);
                    neighbor.PreviousSet.Add(node.Position);

                    // 계단인 경우 추가 공간 예약
                    if (pathCost.isStairs){
                        int xDir = Mathf.Clamp(offset.x, -1, 1);
                        int zDir = Mathf.Clamp(offset.z, -1, 1);
                        Vector3Int verticalOffset = new Vector3Int(0, offset.y, 0);
                        Vector3Int horizontalOffset = new Vector3Int(xDir, 0, zDir);

                        // 계단 설치에 필요한 모든 위치를 이전 방문 집합에 추가
                        neighbor.PreviousSet.Add(node.Position + horizontalOffset);
                        neighbor.PreviousSet.Add(node.Position + horizontalOffset * 2);
                        neighbor.PreviousSet.Add(node.Position + verticalOffset + horizontalOffset);
                        neighbor.PreviousSet.Add(node.Position + verticalOffset + horizontalOffset * 2);
                    }
                }
            }
        }

        return null;  // 경로를 찾지 못한 경우
    }

    // 경로 재구성 메서드 - 목표 노드에서 시작 노드까지의 경로를 역추적
    List<Vector3Int> ReconstructPath(Node node) {
        List<Vector3Int> result = new List<Vector3Int>();

        // 목표 노드에서 시작 노드까지 역순으로 스택에 저장
        while (node != null) {
            stack.Push(node.Position);
            node = node.Previous;
        }

        // 스택에서 꺼내며 올바른 순서로 경로 생성
        while (stack.Count > 0) {
            result.Add(stack.Pop());
        }

        return result;
    }
}
