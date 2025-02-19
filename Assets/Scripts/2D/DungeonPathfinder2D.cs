using System;
using System.Collections.Generic;
using UnityEngine;
using BlueRaja;

public class DungeonPathfinder2D {
    // 경로 탐색에 사용되는 노드 클래스
    public class Node {
        public Vector2Int Position;      // 노드의 위치
        public Node Previous;            // 이전 노드
        public float Cost;              // 시작점에서 현재 노드까지의 비용

        public Node(Vector2Int position) {
            Position = position;
        }
    }

    // 경로 비용을 나타내는 구조체
    public struct PathCost {
        public float cost;              // 경로 비용
        public bool traversable;        // 통과 가능 여부
    }

    // 이웃한 셀들의 상대 위치를 정의
    static readonly Vector2Int[] neighbors = {
        new Vector2Int(1, 0),   // 오른쪽
        new Vector2Int(-1, 0),  // 왼쪽
        new Vector2Int(0, 1),   // 위
        new Vector2Int(0, -1),  // 아래
    };

    private Vector2Int size;            // 던전의 크기
    private Node[,] nodes;              // 노드 배열
    SimplePriorityQueue<Node, float> queue;  // A* 알고리즘용 우선순위 큐
    HashSet<Node> closed;               // 이미 방문한 노드들
    Stack<Vector2Int> stack;            // 경로 재구성용 스택

    // 생성자
    public DungeonPathfinder2D(Vector2Int size) {
        this.size = size;
        nodes = new Node[size.x, size.y];
        
        queue = new SimplePriorityQueue<Node, float>();
        closed = new HashSet<Node>();
        stack = new Stack<Vector2Int>();

        // 모든 위치에 대해 노드 초기화
        for (int y = 0; y < size.y; y++) {
            for (int x = 0; x < size.x; x++) {
                nodes[x, y] = new Node(new Vector2Int(x, y));
            }
        }
    }

    // A* 알고리즘을 사용한 경로 찾기
    public List<Vector2Int> FindPath(Vector2Int start, Vector2Int end, Func<Node, Node, PathCost> costFunction) {
        ResetNodes();  // 노드 상태 초기화
        
        // 시작 노드 설정
        Node startNode = nodes[start.x, start.y];
        startNode.Cost = 0;
        queue.Enqueue(startNode, 0);

        while (queue.Count > 0) {
            Node current = queue.Dequeue();
            
            // 목적지 도달 확인
            if (current.Position == end) {
                return ReconstructPath(current);
            }

            closed.Add(current);

            // 이웃 노드들 탐색
            foreach (var offset in neighbors) {
                Vector2Int nextPos = current.Position + offset;
                
                // 그리드 범위 체크
                if (nextPos.x < 0 || nextPos.x >= size.x || nextPos.y < 0 || nextPos.y >= size.y) continue;
                
                Node next = nodes[nextPos.x, nextPos.y];
                if (closed.Contains(next)) continue;

                // 비용 계산
                var pathCost = costFunction(current, next);
                if (!pathCost.traversable) continue;

                float newCost = current.Cost + pathCost.cost;
                
                if (!queue.Contains(next) || newCost < next.Cost) {
                    next.Cost = newCost;
                    next.Previous = current;
                    queue.Enqueue(next, newCost + Vector2Int.Distance(nextPos, end));
                }
            }
        }

        return null;  // 경로를 찾지 못함
    }

    // 노드들의 상태를 초기화
    private void ResetNodes() {
        queue.Clear();
        closed.Clear();
        
        for (int y = 0; y < size.y; y++) {
            for (int x = 0; x < size.x; x++) {
                var node = nodes[x, y];
                node.Cost = float.MaxValue;
                node.Previous = null;
            }
        }
    }

    // 찾은 경로를 재구성
    private List<Vector2Int> ReconstructPath(Node end) {
        stack.Clear();
        
        Node current = end;
        while (current != null) {
            stack.Push(current.Position);
            current = current.Previous;
        }

        return new List<Vector2Int>(stack);
    }
}
