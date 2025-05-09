﻿using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// 2D 던전 경로 탐색기 클래스
/// A* 알고리즘을 사용한 경로 탐색 구현
/// </summary>
public class DungeonPathfinder2D {
    /// <summary>
    /// 경로 탐색에 사용되는 노드 클래스
    /// </summary>
    public class Node {
        public Vector2Int Position { get; private set; }    // 노드의 2D 위치
        public Node Previous { get; set; }                  // 이전 노드 참조
        public float Cost { get; set; }                     // 시작점에서 현재까지의 누적 비용
        public float Heuristic { get; set; }                // 휴리스틱 함수 값
        public bool IsRoom { get; set; }                    // 해당 위치가 방인지 여부
        public bool IsCorridor { get; set; }                // 해당 위치가 복도인지 여부

        /// <summary>
        /// 노드 생성자
        /// </summary>
        /// <param name="position">노드의 2D 위치</param>
        public Node(Vector2Int position) {
            Position = position;
            IsRoom = false;
            IsCorridor = false;
        }
    }

    /// <summary>
    /// 경로 비용 정보를 담는 구조체
    /// </summary>
    public struct PathCost {
        public float cost;              // 경로 비용
        public bool traversable;        // 통과 가능 여부
        public bool isRoom;             // 해당 위치가 방인지 여부
        public bool isCorridor;         // 해당 위치가 복도인지 여부
    }

    /// <summary>
    /// 이웃 노드들의 상대적 위치 정의
    /// 기본 이동(동서남북) 방향 포함
    /// </summary>
    static readonly Vector2Int[] neighbors = {
        new Vector2Int(1, 0),   // 오른쪽
        new Vector2Int(-1, 0),  // 왼쪽
        new Vector2Int(0, 1),   // 위
        new Vector2Int(0, -1),  // 아래
    };

    private Vector2Int size;            // 던전의 크기
    private Node[,] nodes;              // 노드 배열
    private PriorityQueue queue;        // 최적화된 우선순위 큐
    private HashSet<Node> closed;       // 이미 방문한 노드들
    private Vector2Int endPos;          // 목표 위치

    /// <summary>
    /// 경로 탐색기 생성자
    /// </summary>
    /// <param name="size">던전의 2차원 크기</param>
    public DungeonPathfinder2D(Vector2Int size) {
        this.size = size;
        nodes = new Node[size.x, size.y];
        queue = new PriorityQueue();
        closed = new HashSet<Node>();

        // 필요한 노드만 동적으로 생성
        for (int y = 0; y < size.y; y++) {
            for (int x = 0; x < size.x; x++) {
                nodes[x, y] = new Node(new Vector2Int(x, y));
            }
        }
    }

    /// <summary>
    /// 모든 노드의 상태를 초기화
    /// </summary>
    private void ResetNodes() {
        queue.Clear();
        closed.Clear();
        
        // 모든 노드 초기화
        for (int y = 0; y < size.y; y++) {
            for (int x = 0; x < size.x; x++) {
                var node = nodes[x, y];
                node.Cost = float.MaxValue;
                node.Previous = null;
            }
        }
    }

    /// <summary>
    /// 휴리스틱 함수 최적화
    /// 유클리드 거리를 사용한 휴리스틱 계산
    /// </summary>
    /// <param name="current">현재 위치</param>
    /// <param name="end">목표 위치</param>
    /// <returns>휴리스틱 값</returns>
    private float CalculateHeuristic(Vector2Int current, Vector2Int end) {
        return Vector2Int.Distance(current, end);
    }

    /// <summary>
    /// 시작점에서 목표점까지의 경로를 찾는 메서드
    /// </summary>
    /// <param name="start">시작 위치</param>
    /// <param name="end">목표 위치</param>
    /// <param name="costFunction">노드 간 이동 비용을 계산하는 함수</param>
    /// <returns>경로를 이루는 위치들의 리스트</returns>
    public List<Vector2Int> FindPath(Vector2Int start, Vector2Int end, Func<Node, Node, PathCost> costFunction) {
        ResetNodes();
        endPos = end;
        
        Node startNode = nodes[start.x, start.y];
        startNode.Cost = 0;
        startNode.Heuristic = CalculateHeuristic(start, end);
        queue.Enqueue(startNode, startNode.Heuristic);

        while (queue.Count > 0) {
            Node node = queue.Dequeue();

            if (node.Position == end) {
                return ReconstructPath(node);
            }

            if (closed.Contains(node)) continue;
            closed.Add(node);

            // 방향성 있는 노드 검색 최적화
            Vector2 direction = (end - node.Position).ToVector2();
            var sortedNeighbors = neighbors.OrderBy(n => 
                Vector2.Dot(n.ToVector2(), direction.normalized)).ToList();

            foreach (var offset in sortedNeighbors) {
                Vector2Int nextPos = node.Position + offset;
                
                if (nextPos.x < 0 || nextPos.x >= size.x || nextPos.y < 0 || nextPos.y >= size.y) continue;
                
                Node neighbor = nodes[nextPos.x, nextPos.y];
                if (closed.Contains(neighbor)) continue;

                var pathCost = costFunction(node, neighbor);
                
                // 방과 복도의 겹침에 대한 추가 비용 계산
                float additionalCost = 0f;
                
                // 방에서 복도로 이동하거나 복도에서 방으로 이동하는 경우
                if ((pathCost.isRoom && node.IsCorridor) || (pathCost.isCorridor && node.IsRoom)) {
                    // 높은 추가 비용 부여 (기본 비용의 2배)
                    additionalCost = pathCost.cost * 2f;
                }
                
                if (!pathCost.traversable) continue;

                float newCost = node.Cost + pathCost.cost + additionalCost;
                
                if (!queue.Contains(neighbor) || newCost < neighbor.Cost) {
                    neighbor.Cost = newCost;
                    neighbor.Heuristic = CalculateHeuristic(nextPos, end);
                    neighbor.Previous = node;
                    neighbor.IsRoom = pathCost.isRoom;
                    neighbor.IsCorridor = pathCost.isCorridor;
                    
                    if (queue.Contains(neighbor)) {
                        queue.UpdatePriority(neighbor, newCost + neighbor.Heuristic);
                    } else {
                        queue.Enqueue(neighbor, newCost + neighbor.Heuristic);
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 경로 재구성 최적화
    /// 경로 길이를 미리 계산하여 리스트 크기 할당
    /// </summary>
    /// <param name="end">목표 노드</param>
    /// <returns>경로를 이루는 위치들의 리스트</returns>
    private List<Vector2Int> ReconstructPath(Node end) {
        // 경로 길이 계산
        int length = 0;
        Node current = end;
        while (current != null) {
            length++;
            current = current.Previous;
        }
        
        // 미리 크기 할당
        var path = new List<Vector2Int>(length);
        current = end;
        while (current != null) {
            path.Add(current.Position);
            current = current.Previous;
        }
        path.Reverse(); // 마지막에 한 번만 뒤집기
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
/// Vector2Int 확장 메서드
/// Vector2Int를 Vector2로 변환하는 기능 제공
/// </summary>
public static class Vector2IntExtensions {
    /// <summary>
    /// Vector2Int를 Vector2로 변환
    /// </summary>
    /// <param name="v">변환할 Vector2Int</param>
    /// <returns>변환된 Vector2</returns>
    public static Vector2 ToVector2(this Vector2Int v) {
        return new Vector2(v.x, v.y);
    }
}
