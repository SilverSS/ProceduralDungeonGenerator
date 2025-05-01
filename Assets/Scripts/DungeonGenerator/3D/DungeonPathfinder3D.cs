using System;
using System.Collections.Generic;
using UnityEngine;
using BlueRaja;
using System.Linq;

/// <summary>
/// 3D 던전 경로 탐색기 클래스
/// 계단 배치를 위한 수정된 A* 알고리즘을 구현합니다.
/// 이 클래스는 3차원 공간에서 두 방 사이의 최적 경로를 찾고,
/// 필요한 경우 적절한 위치에 계단을 배치합니다.
/// </summary>
public class DungeonPathfinder3D {
    /// <summary>
    /// 경로 탐색에 사용되는 노드 클래스
    /// 각 노드는 3D 공간의 한 지점을 나타내며, 경로 탐색에 필요한 모든 정보를 포함합니다.
    /// </summary>
    public class Node {
        public Vector3Int Position { get; private set; }    // 노드의 3D 위치 (그리드 좌표)
        public Node Previous { get; set; }                  // 최적 경로상 이전 노드의 참조
        public HashSet<Vector3Int> PreviousSet { get; set; }  // 이전에 방문한 모든 위치들의 집합 (순환 방지용)
        public float Cost { get; set; }                     // 시작점에서 현재 노드까지의 실제 누적 비용
        public float Heuristic { get; set; }                // 현재 노드에서 목표까지의 예상 비용 (휴리스틱)
        public bool IsRoom { get; set; }                    // 해당 위치가 방인지 여부
        public bool IsCorridor { get; set; }                // 해당 위치가 복도인지 여부
        public bool IsStairs { get; set; }                  // 해당 위치가 계단인지 여부
        public HashSet<Vector3Int> StairCells { get; set; } // 이 노드가 계단일 경우, 계단이 차지하는 모든 셀의 위치

        /// <summary>
        /// 노드 생성자
        /// 새로운 노드를 초기화하고 필요한 컬렉션들을 생성합니다.
        /// </summary>
        /// <param name="position">노드의 3D 그리드 위치</param>
        public Node(Vector3Int position) {
            Position = position;
            PreviousSet = new HashSet<Vector3Int>();  // 방문 기록 추적용 집합 초기화
            StairCells = new HashSet<Vector3Int>();   // 계단 셀 추적용 집합 초기화
            IsRoom = false;      // 초기에는 방이 아님
            IsCorridor = false;  // 초기에는 복도가 아님
            IsStairs = false;    // 초기에는 계단이 아님
        }

        /// <summary>
        /// 계단 셀들을 노드에 추가
        /// 계단이 차지하는 모든 셀을 기록하고, 이를 방문 기록에도 추가합니다.
        /// </summary>
        /// <param name="cells">계단이 차지하는 셀들의 위치 목록</param>
        public void AddStairCells(IEnumerable<Vector3Int> cells) {
            foreach (var cell in cells) {
                StairCells.Add(cell);         // 계단 셀로 등록
                PreviousSet.Add(cell);        // 방문 기록에도 추가하여 재방문 방지
            }
        }
    }

    /// <summary>
    /// 경로 비용 정보를 담는 구조체
    /// 두 노드 사이의 이동 가능 여부와 비용을 정의합니다.
    /// </summary>
    public struct PathCost {
        public bool traversable;    // 해당 위치로 이동이 가능한지 여부
        public float cost;          // 이동에 필요한 기본 비용
        public bool isStairs;       // 계단 설치가 가능한 위치인지 여부
        public bool isRoom;         // 해당 위치가 방인지 여부
        public bool isCorridor;     // 해당 위치가 복도인지 여부
    }

    /// <summary>
    /// 이동 가능한 방향을 정의하는 상수 배열
    /// 우선순위 순서대로 정렬되어 있음:
    /// 1. 수평 이동 (가장 높은 우선순위)
    /// 2. 위로 가는 계단 (중간 우선순위)
    /// 3. 아래로 가는 계단 (낮은 우선순위)
    /// </summary>
    static readonly Vector3Int[] neighbors = {
        // 기본 수평 이동 방향 (가장 높은 우선순위)
        new Vector3Int(1, 0, 0),   // 동쪽으로 1칸
        new Vector3Int(-1, 0, 0),  // 서쪽으로 1칸
        new Vector3Int(0, 0, 1),   // 북쪽으로 1칸
        new Vector3Int(0, 0, -1),  // 남쪽으로 1칸

        // 위로 가는 계단 (중간 우선순위)
        new Vector3Int(3, 1, 0),   // 동쪽으로 3칸 + 위로 1칸
        new Vector3Int(-3, 1, 0),  // 서쪽으로 3칸 + 위로 1칸
        new Vector3Int(0, 1, 3),   // 북쪽으로 3칸 + 위로 1칸
        new Vector3Int(0, 1, -3),  // 남쪽으로 3칸 + 위로 1칸

        // 아래로 가는 계단 (낮은 우선순위)
        new Vector3Int(3, -1, 0),  // 동쪽으로 3칸 + 아래로 1칸
        new Vector3Int(-3, -1, 0), // 서쪽으로 3칸 + 아래로 1칸
        new Vector3Int(0, -1, 3),  // 북쪽으로 3칸 + 아래로 1칸
        new Vector3Int(0, -1, -3), // 남쪽으로 3칸 + 아래로 1칸
    };

    // 경로 탐색에 필요한 데이터 구조들
    private Grid3D<Node> grid;                              // 3D 공간의 모든 노드를 저장하는 그리드
    private PriorityQueue queue;                            // 최적화된 우선순위 큐 (다음 방문할 노드 선택용)
    private HashSet<Node> closed;                           // 이미 방문한 노드들의 집합 (재방문 방지)
    private Vector3Int endPos;                              // 목표 위치 (휴리스틱 계산에 사용)

    /// <summary>
    /// 계단 설치가 필요한지 확인하는 메서드
    /// </summary>
    /// <param name="current">현재 위치</param>
    /// <param name="end">목표 위치</param>
    /// <returns>계단이 필요하면 true, 아니면 false</returns>
    /// <remarks>
    /// 1. 현재 위치와 목표 위치의 높이 차이를 확인
    /// 2. 높이 차이가 있는 경우 계단 설치 필요
    /// 3. 같은 높이면 계단 불필요
    /// </remarks>
    private bool NeedsStairs(Vector3Int current, Vector3Int end) {
        // 현재 위치와 목표 위치의 높이가 같으면 계단 불필요
        if (current.y == end.y) return false;

        // 이미 목표 높이에 도달했다면 계단 불필요
        // (위로 가는 중에 목표 높이 이상이 되었거나, 아래로 가는 중에 목표 높이 이하가 된 경우)
        if ((current.y < end.y && current.y >= end.y) || 
            (current.y > end.y && current.y <= end.y)) {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 계단이 차지하는 모든 셀의 위치를 계산하는 메서드
    /// </summary>
    /// <param name="start">시작 위치</param>
    /// <param name="offset">이동 방향과 거리</param>
    /// <returns>계단을 구성하는 모든 셀의 위치 배열</returns>
    /// <remarks>
    /// 계단 구성:
    /// 1. 시작 위치
    /// 2. 첫 번째 수평 이동
    /// 3. 두 번째 수평 이동
    /// 4. 첫 번째 수평+수직 이동
    /// 5. 두 번째 수평+수직 이동
    /// 6. 최종 도착 위치
    /// </remarks>
    private Vector3Int[] GetStairCells(Vector3Int start, Vector3Int offset) {
        // 계단의 방향 벡터 계산 (단위 벡터로 변환)
        int xDir = Mathf.Clamp(offset.x, -1, 1);
        int zDir = Mathf.Clamp(offset.z, -1, 1);
        Vector3Int verticalOffset = new Vector3Int(0, offset.y, 0);       // 수직 이동량
        Vector3Int horizontalOffset = new Vector3Int(xDir, 0, zDir);      // 수평 이동량

        // 계단이 차지하는 모든 셀의 위치 반환
        return new Vector3Int[] {
            start,                                          // 시작 위치
            start + horizontalOffset,                       // 첫 번째 수평 이동
            start + horizontalOffset * 2,                   // 두 번째 수평 이동
            start + verticalOffset + horizontalOffset,      // 첫 번째 수평+수직 이동
            start + verticalOffset + horizontalOffset * 2,  // 두 번째 수평+수직 이동
            start + offset                                  // 최종 도착 위치
        };
    }

    /// <summary>
    /// 현재 상황에 적합한 이웃 노드들의 오프셋을 가져오는 메서드
    /// </summary>
    /// <param name="current">현재 위치</param>
    /// <param name="end">목표 위치</param>
    /// <returns>사용 가능한 이동 방향들의 열거형</returns>
    /// <remarks>
    /// 이동 방향 결정 로직:
    /// 1. 같은 높이의 경우: 수평 이동만 허용 (동,서,남,북)
    /// 2. 위로 이동해야 하는 경우: 수평 이동 + 상향 계단 옵션
    /// 3. 아래로 이동해야 하는 경우: 수평 이동 + 하향 계단 옵션
    /// 
    /// 최적화 고려사항:
    /// - 불필요한 계단 생성 방지
    /// - 자연스러운 경로 생성을 위한 우선순위 적용
    /// - 이전 경로와의 충돌 방지
    /// </remarks>
    private IEnumerable<Vector3Int> GetAvailableNeighbors(Vector3Int current, Vector3Int end) {
        // 현재 위치와 목표 위치의 높이가 같으면 수평 이동만 반환
        if (current.y == end.y) {
            yield return new Vector3Int(1, 0, 0);   // 동쪽으로 1칸
            yield return new Vector3Int(-1, 0, 0);  // 서쪽으로 1칸
            yield return new Vector3Int(0, 0, 1);   // 북쪽으로 1칸
            yield return new Vector3Int(0, 0, -1);  // 남쪽으로 1칸
            yield break;  // 수평 이동만 허용하고 종료
        }

        // 위로 가야하는 경우
        if (current.y < end.y) {
            // 기본 수평 이동 옵션
            yield return new Vector3Int(1, 0, 0);   // 동쪽으로 1칸
            yield return new Vector3Int(-1, 0, 0);  // 서쪽으로 1칸
            yield return new Vector3Int(0, 0, 1);   // 북쪽으로 1칸
            yield return new Vector3Int(0, 0, -1);  // 남쪽으로 1칸

            // 위로 가는 계단 옵션
            yield return new Vector3Int(3, 1, 0);   // 동쪽으로 3칸 + 위로 1칸
            yield return new Vector3Int(-3, 1, 0);  // 서쪽으로 3칸 + 위로 1칸
            yield return new Vector3Int(0, 1, 3);   // 북쪽으로 3칸 + 위로 1칸
            yield return new Vector3Int(0, 1, -3);  // 남쪽으로 3칸 + 위로 1칸
        }
        // 아래로 가야하는 경우
        else if (current.y > end.y) {
            // 기본 수평 이동 옵션
            yield return new Vector3Int(1, 0, 0);   // 동쪽으로 1칸
            yield return new Vector3Int(-1, 0, 0);  // 서쪽으로 1칸
            yield return new Vector3Int(0, 0, 1);   // 북쪽으로 1칸
            yield return new Vector3Int(0, 0, -1);  // 남쪽으로 1칸

            // 아래로 가는 계단 옵션
            yield return new Vector3Int(3, -1, 0);  // 동쪽으로 3칸 + 아래로 1칸
            yield return new Vector3Int(-3, -1, 0); // 서쪽으로 3칸 + 아래로 1칸
            yield return new Vector3Int(0, -1, 3);  // 북쪽으로 3칸 + 아래로 1칸
            yield return new Vector3Int(0, -1, -3); // 남쪽으로 3칸 + 아래로 1칸
        }
    }

    /// <summary>
    /// 개선된 비용 계산 로직
    /// 이동 거리와 높이 차이를 고려하여 경로의 비용을 계산합니다.
    /// </summary>
    /// <param name="current">현재 위치</param>
    /// <param name="end">목표 위치</param>
    /// <param name="isStairs">계단 여부</param>
    /// <returns>계산된 경로 비용</returns>
    private float CalculatePathCost(Vector3Int current, Vector3Int end, bool isStairs) {
        // 수평 거리 계산 (피타고라스 정리 사용)
        float horizontalCost = Mathf.Sqrt(
            Mathf.Pow(current.x - end.x, 2) + 
            Mathf.Pow(current.z - end.z, 2)
        );
        
        // 수직 거리 계산
        float verticalCost = Mathf.Abs(current.y - end.y);
        
        // 수직 이동이 필요하고 계단이 필요한 경우에만 계단 관련 비용 조정
        if (verticalCost > 0 && NeedsStairs(current, end)) {
            if (!isStairs) {
                // 계단이 필요한데 수평 이동하는 경우 높은 페널티
                horizontalCost *= 2.0f;
            } else {
                // 계단 설치는 약간 우대 (비용 감소)
                horizontalCost *= 0.8f;
            }
        }
        
        // 최종 비용 반환 (수평 비용 + 수직 비용 * 2)
        return horizontalCost + verticalCost * 2;
    }

    /// <summary>
    /// 개선된 방향성 결정 로직
    /// 현재 위치에서 목표까지의 선호되는 이동 방향을 계산합니다.
    /// </summary>
    /// <param name="current">현재 위치</param>
    /// <param name="end">목표 위치</param>
    /// <returns>정규화된 선호 방향 벡터</returns>
    private Vector3 GetPreferredDirection(Vector3Int current, Vector3Int end) {
        // 기본 방향 벡터 계산
        Vector3 direction = (end - current).ToVector3();
        
        // 계단이 필요한 경우에만 수직 이동 우대
        if (NeedsStairs(current, end)) {
            // y 방향 가중치 증가
            direction.y *= 2f;
            
            // 목표 지점까지의 수평 거리 계산
            float horizontalDistance = Mathf.Sqrt(
                Mathf.Pow(end.x - current.x, 2) + 
                Mathf.Pow(end.z - current.z, 2)
            );
            
            // 수평 거리가 가까우면 수직 이동 추가 우대
            if (horizontalDistance < 3f) {
                direction.y *= 2f;
            }
        }
        
        // 방향 벡터 정규화하여 반환
        return direction.normalized;
    }

    /// <summary>
    /// 휴리스틱 함수 최적화
    /// 현재 위치에서 목표까지의 예상 비용을 계산합니다.
    /// </summary>
    /// <param name="current">현재 위치</param>
    /// <param name="end">목표 위치</param>
    /// <returns>예상 비용 (휴리스틱 값)</returns>
    private float CalculateHeuristic(Vector3Int current, Vector3Int end) {
        // 각 축의 거리 계산
        float dx = Mathf.Abs(current.x - end.x);
        float dy = Mathf.Abs(current.y - end.y) * 2.0f;  // 수직 이동에 2배 가중치
        float dz = Mathf.Abs(current.z - end.z);
        
        // 높이 차이가 있는 경우 수직 이동에 추가 가중치
        if (current.y != end.y) {
            dy *= 1.5f;
        }
        
        // 3D 유클리드 거리와 수직 이동 비용의 조합
        return Mathf.Sqrt(dx * dx + dz * dz) + dy;
    }

    /// <summary>
    /// 경로 탐색기 생성자
    /// 3D 공간의 크기에 맞춰 필요한 데이터 구조를 초기화합니다.
    /// </summary>
    /// <param name="size">던전의 3차원 크기</param>
    public DungeonPathfinder3D(Vector3Int size) {
        // 3D 그리드 초기화
        grid = new Grid3D<Node>(size, Vector3Int.zero);
        queue = new PriorityQueue();
        closed = new HashSet<Node>();

        // 모든 그리드 위치에 대한 노드 생성
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
    /// 모든 노드의 상태를 초기화하는 메서드
    /// 새로운 경로 탐색을 시작하기 전에 호출됩니다.
    /// </summary>
    private void ResetNodes() {
        // 탐색 관련 데이터 구조 초기화
        queue.Clear();
        closed.Clear();
        
        // 모든 노드 상태 초기화
        for (int z = 0; z < grid.Size.z; z++) {
            for (int y = 0; y < grid.Size.y; y++) {
                for (int x = 0; x < grid.Size.x; x++) {
                    var node = grid[x, y, z];
                    node.Cost = float.MaxValue;        // 비용을 무한대로 초기화
                    node.Previous = null;              // 이전 노드 참조 제거
                    node.PreviousSet.Clear();          // 방문 기록 초기화
                    node.IsStairs = false;            // 계단 상태 초기화
                }
            }
        }
    }

    /// <summary>
    /// A* 알고리즘을 사용한 경로 탐색 메서드
    /// </summary>
    /// <param name="start">시작 위치</param>
    /// <param name="end">목표 위치</param>
    /// <param name="costFunction">이동 비용 계산 함수</param>
    /// <returns>시작점에서 목표점까지의 최적 경로</returns>
    /// <remarks>
    /// 경로 탐색 과정:
    /// 1. 시작 노드 초기화 및 우선순위 큐에 추가
    /// 2. 현재 최선의 노드 선택
    /// 3. 목표 도달 확인
    /// 4. 이웃 노드 탐색 및 비용 계산
    /// 5. 더 나은 경로 발견 시 노드 정보 업데이트
    /// 
    /// 최적화 기능:
    /// - 방향성 있는 노드 검색으로 효율적인 경로 생성
    /// - 방과 복도 전환 시 추가 비용 부여
    /// - 계단 설치 위치 최적화
    /// - 이전 경로와의 충돌 방지
    /// </remarks>
    public List<Vector3Int> FindPath(Vector3Int start, Vector3Int end, Func<Node, Node, PathCost> costFunction) {
        // 탐색 준비
        ResetNodes();
        endPos = end;
        
        // 시작 노드 초기화
        Node startNode = grid[start];
        startNode.Cost = 0;
        startNode.Heuristic = CalculateHeuristic(start, end);
        queue.Enqueue(startNode, startNode.Heuristic);

        while (queue.Count > 0) {
            // 현재 최선의 노드 선택
            Node node = queue.Dequeue();

            // 목표 도달 확인
            if (node.Position == end) {
                return ReconstructPath(node);
            }

            // 이미 방문한 노드는 건너뛰기
            if (closed.Contains(node)) continue;
            closed.Add(node);

            // 현재 상황에 적합한 이웃 노드들만 가져오기
            var availableNeighbors = GetAvailableNeighbors(node.Position, end);
            
            // 개선된 방향성 결정 사용
            Vector3 preferredDirection = GetPreferredDirection(node.Position, end);
            var sortedNeighbors = availableNeighbors.OrderBy(n => {
                float dot = Vector3.Dot(n.ToVector3().normalized, preferredDirection);
                // 계단이 필요한 경우에만 계단 방향 우대
                if (NeedsStairs(node.Position, end) && n.y != 0) {
                    dot *= 1.2f;
                }
                return -dot;  // 내림차순 정렬
            }).ToList();

            // 각 이웃 노드 처리
            foreach (var offset in sortedNeighbors) {
                Vector3Int nextPos = node.Position + offset;
                
                // 그리드 범위 검사
                if (!grid.InBounds(nextPos)) continue;
                
                Node neighbor = grid[nextPos];
                if (closed.Contains(neighbor)) continue;

                // 이전에 방문한 위치나 계단 셀은 제외
                if (node.PreviousSet.Contains(neighbor.Position) || 
                    node.StairCells.Contains(neighbor.Position)) {
                    continue;
                }

                // 이동 가능성과 비용 계산
                var pathCost = costFunction(node, neighbor);
                if (!pathCost.traversable) continue;

                // 계단인 경우 모든 계단 셀 확인
                if (pathCost.isStairs) {
                    var stairCells = GetStairCells(node.Position, offset);
                    // 계단 셀 중 하나라도 이미 사용 중이면 이 경로는 사용 불가
                    if (stairCells.Any(cell => node.PreviousSet.Contains(cell) || 
                                             node.StairCells.Contains(cell))) {
                        continue;
                    }
                    neighbor.AddStairCells(stairCells);
                }
                
                // 이동 비용 계산
                float movementCost = CalculatePathCost(nextPos, end, pathCost.isStairs);
                float additionalCost = 0f;
                
                // 방과 복도 간 전환에 대한 추가 비용
                if ((pathCost.isRoom && node.IsCorridor) || (pathCost.isCorridor && node.IsRoom)) {
                    additionalCost = movementCost * 2f;
                }

                // 총 비용 계산
                float newCost = node.Cost + movementCost + additionalCost;
                
                // 더 나은 경로를 찾은 경우 노드 정보 업데이트
                if (!queue.Contains(neighbor) || newCost < neighbor.Cost) {
                    neighbor.Cost = newCost;
                    neighbor.Heuristic = CalculateHeuristic(nextPos, end);
                    neighbor.Previous = node;
                    neighbor.PreviousSet = new HashSet<Vector3Int>(node.PreviousSet);
                    neighbor.PreviousSet.Add(node.Position);
                    neighbor.IsRoom = pathCost.isRoom;
                    neighbor.IsCorridor = pathCost.isCorridor;
                    neighbor.IsStairs = pathCost.isStairs;
                    
                    // 우선순위 큐 업데이트
                    if (queue.Contains(neighbor)) {
                        queue.UpdatePriority(neighbor, newCost + neighbor.Heuristic);
                    } else {
                        queue.Enqueue(neighbor, newCost + neighbor.Heuristic);
                    }
                }
            }
        }

        // 경로를 찾지 못한 경우
        return null;
    }

    /// <summary>
    /// 경로 재구성 최적화 메서드
    /// </summary>
    /// <param name="end">목표 노드</param>
    /// <returns>최적화된 경로 리스트</returns>
    /// <remarks>
    /// 최적화 전략:
    /// 1. 경로 길이 미리 계산하여 리스트 크기 할당
    /// 2. 역방향 추적으로 경로 구성
    /// 3. 최종 경로를 정방향으로 변환
    /// </remarks>
    private List<Vector3Int> ReconstructPath(Node end) {
        // 경로 길이 계산
        int length = 0;
        Node current = end;
        while (current != null) {
            length++;
            current = current.Previous;
        }
        
        // 경로 재구성 (역순)
        var path = new List<Vector3Int>(length);
        current = end;
        while (current != null) {
            path.Add(current.Position);
            current = current.Previous;
        }
        
        // 경로를 정순으로 변경
        path.Reverse();
        return path;
    }

    /// <summary>
    /// 최적화된 우선순위 큐 구현
    /// 이진 힙을 사용한 효율적인 우선순위 큐
    /// </summary>
    private class PriorityQueue {
        private List<(Node, float)> items = new List<(Node, float)>();      // (노드, 우선순위) 쌍을 저장하는 리스트
        private Dictionary<Node, int> nodeIndices = new Dictionary<Node, int>();  // 노드의 인덱스를 저장하는 딕셔너리

        public int Count => items.Count;  // 큐의 크기

        /// <summary>
        /// 큐 초기화
        /// </summary>
        public void Clear() {
            items.Clear();
            nodeIndices.Clear();
        }

        /// <summary>
        /// 노드가 큐에 있는지 확인
        /// </summary>
        public bool Contains(Node node) {
            return nodeIndices.ContainsKey(node);
        }

        /// <summary>
        /// 노드를 큐에 추가
        /// </summary>
        public void Enqueue(Node node, float priority) {
            items.Add((node, priority));
            int i = items.Count - 1;
            nodeIndices[node] = i;
            HeapifyUp(i);
        }

        /// <summary>
        /// 우선순위가 가장 높은 노드를 큐에서 제거하고 반환
        /// </summary>
        public Node Dequeue() {
            if (items.Count == 0) throw new InvalidOperationException("Queue is empty");
            
            var result = items[0].Item1;
            nodeIndices.Remove(result);
            
            if (items.Count > 1) {
                items[0] = items[items.Count - 1];
                nodeIndices[items[0].Item1] = 0;
            }
            
            items.RemoveAt(items.Count - 1);
            
            if (items.Count > 0) {
                HeapifyDown(0);
            }
            
            return result;
        }

        /// <summary>
        /// 노드의 우선순위 업데이트
        /// </summary>
        public void UpdatePriority(Node node, float newPriority) {
            if (!nodeIndices.TryGetValue(node, out int index)) {
                throw new InvalidOperationException("Node not found in queue");
            }
            
            float oldPriority = items[index].Item2;
            items[index] = (node, newPriority);
            
            if (newPriority < oldPriority) {
                HeapifyUp(index);
            } else {
                HeapifyDown(index);
            }
        }

        /// <summary>
        /// 힙 속성 유지를 위한 상향 이동
        /// </summary>
        private void HeapifyUp(int i) {
            while (i > 0) {
                int parent = (i - 1) / 2;
                if (items[parent].Item2 <= items[i].Item2) break;
                
                Swap(i, parent);
                i = parent;
            }
        }

        /// <summary>
        /// 힙 속성 유지를 위한 하향 이동
        /// </summary>
        private void HeapifyDown(int i) {
            while (true) {
                int left = 2 * i + 1;
                int right = 2 * i + 2;
                int smallest = i;
                
                if (left < items.Count && items[left].Item2 < items[smallest].Item2) {
                    smallest = left;
                }
                
                if (right < items.Count && items[right].Item2 < items[smallest].Item2) {
                    smallest = right;
                }
                
                if (smallest == i) break;
                
                Swap(i, smallest);
                i = smallest;
            }
        }

        /// <summary>
        /// 두 노드의 위치 교환
        /// </summary>
        private void Swap(int i, int j) {
            var temp = items[i];
            items[i] = items[j];
            items[j] = temp;
            
            nodeIndices[items[i].Item1] = i;
            nodeIndices[items[j].Item1] = j;
        }
    }
}

/// <summary>
/// Vector3Int 확장 메서드
/// Vector3Int를 Vector3로 변환하는 기능 제공
/// </summary>
public static class Vector3IntExtensions {
    /// <summary>
    /// Vector3Int를 Vector3로 변환
    /// </summary>
    /// <param name="v">변환할 Vector3Int</param>
    /// <returns>변환된 Vector3</returns>
    public static Vector3 ToVector3(this Vector3Int v) {
        return new Vector3(v.x, v.y, v.z);
    }
}
