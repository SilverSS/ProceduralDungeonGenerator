using System;
using System.Collections.Generic;
using UnityEngine;
using Graphs;

/// <summary>
/// 프림 알고리즘을 구현한 정적 클래스
/// 최소 신장 트리(Minimum Spanning Tree)를 생성하여 던전의 방들을 연결하는 최적의 경로를 찾습니다.
/// </summary>
public static class Prim {
    /// <summary>
    /// 프림 알고리즘에서 사용되는 간선 클래스
    /// 두 정점 간의 거리 정보를 포함합니다.
    /// </summary>
    public class Edge : Graphs.Edge {
        public float Distance { get; private set; }    // 두 정점 사이의 유클리드 거리

        // 생성자: 두 정점 사이의 거리를 자동으로 계산
        public Edge(Vertex u, Vertex v) : base(u, v) {
            Distance = Vector3.Distance(u.Position, v.Position);
        }

        // 두 간선이 같은지 비교하는 연산자 (방향에 관계없이 같은 정점들을 연결하면 동일한 간선으로 취급)
        public static bool operator ==(Edge left, Edge right) {
            return (left.U == right.U && left.V == right.V)
                || (left.U == right.V && left.V == right.U);
        }

        public static bool operator !=(Edge left, Edge right) {
            return !(left == right);
        }

        // object.Equals 오버라이드
        public override bool Equals(object obj) {
            if (obj is Edge e) {
                return this == e;
            }
            return false;
        }

        // Edge 타입과의 비교를 위한 Equals 메서드
        public bool Equals(Edge e) {
            return this == e;
        }

        // 해시 테이블에서 사용될 해시 코드 생성 (두 정점의 해시값을 XOR 연산)
        public override int GetHashCode() {
            return U.GetHashCode() ^ V.GetHashCode();
        }
    }

    /// <summary>
    /// 프림 알고리즘을 사용하여 최소 신장 트리를 생성하는 메서드
    /// </summary>
    /// <param name="edges">가능한 모든 간선들의 리스트</param>
    /// <param name="start">시작 정점</param>
    /// <returns>최소 신장 트리를 구성하는 간선들의 리스트</returns>
    public static List<Edge> MinimumSpanningTree(List<Edge> edges, Vertex start) {
        // 아직 트리에 포함되지 않은 정점들의 집합
        HashSet<Vertex> openSet = new HashSet<Vertex>();
        // 이미 트리에 포함된 정점들의 집합
        HashSet<Vertex> closedSet = new HashSet<Vertex>();

        // 모든 정점을 openSet에 추가
        foreach (var edge in edges) {
            openSet.Add(edge.U);
            openSet.Add(edge.V);
        }

        // 시작 정점을 closedSet으로 이동
        closedSet.Add(start);

        // 최소 신장 트리를 구성할 간선들을 저장할 리스트
        List<Edge> results = new List<Edge>();

        // 모든 정점이 연결될 때까지 반복
        while (openSet.Count > 0) {
            bool chosen = false;
            Edge chosenEdge = null;
            float minWeight = float.PositiveInfinity;

            // 현재 트리에서 가장 가까운 정점을 찾기
            foreach (var edge in edges) {
                // 한쪽 정점만 트리에 포함된 간선 찾기
                int closedVertices = 0;
                if (!closedSet.Contains(edge.U)) closedVertices++;
                if (!closedSet.Contains(edge.V)) closedVertices++;
                if (closedVertices != 1) continue;

                // 최소 가중치 간선 선택
                if (edge.Distance < minWeight) {
                    chosenEdge = edge;
                    chosen = true;
                    minWeight = edge.Distance;
                }
            }

            // 더 이상 연결할 수 없는 경우 종료
            if (!chosen) break;

            // 선택된 간선을 결과에 추가하고 정점 집합 업데이트
            results.Add(chosenEdge);
            openSet.Remove(chosenEdge.U);
            openSet.Remove(chosenEdge.V);
            closedSet.Add(chosenEdge.U);
            closedSet.Add(chosenEdge.V);
        }

        return results;
    }
}
