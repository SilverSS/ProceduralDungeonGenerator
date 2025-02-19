using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 그래프 데이터 구조 및 알고리즘을 위한 네임스페이스
/// </summary>
namespace Graphs {
    /// <summary>
    /// 그래프의 정점을 나타내는 기본 클래스
    /// Vector3를 래핑하여 그래프 알고리즘에 필요한 정점 비교 연산을 제공합니다.
    /// 
    /// 주요 목적:
    /// 1. IEquatable<Vertex> 인터페이스 구현을 통한 정점 비교 연산
    /// 2. 들로네 삼각분할, 최소 신장 트리 등의 그래프 알고리즘에서 필요한 정점 동일성 검사
    /// 3. 제네릭 타입 Vertex<T>를 통해 추가 데이터(예: Room)를 포함하는 정점 확장 가능
    /// </summary>
    public class Vertex : IEquatable<Vertex> {
        public Vector3 Position { get; private set; }

        public Vertex() {

        }

        public Vertex(Vector3 position) {
            Position = position;
        }

        public override bool Equals(object obj) {
            if (obj is Vertex v) {
                return Position == v.Position;
            }

            return false;
        }

        public bool Equals(Vertex other) {
            return Position == other.Position;
        }

        public override int GetHashCode() {
            return Position.GetHashCode();
        }
    }

    public class Vertex<T> : Vertex {
        public T Item { get; private set; }

        public Vertex(T item) {
            Item = item;
        }

        public Vertex(Vector3 position, T item) : base(position) {
            Item = item;
        }
    }

    public class Edge : IEquatable<Edge> {
        public Vertex U { get; set; }
        public Vertex V { get; set; }

        public Edge() {

        }

        public Edge(Vertex u, Vertex v) {
            U = u;
            V = v;
        }

        public static bool operator ==(Edge left, Edge right) {
            return (left.U == right.U || left.U == right.V)
                && (left.V == right.U || left.V == right.V);
        }

        public static bool operator !=(Edge left, Edge right) {
            return !(left == right);
        }

        public override bool Equals(object obj) {
            if (obj is Edge e) {
                return this == e;
            }

            return false;
        }

        public bool Equals(Edge e) {
            return this == e;
        }

        public override int GetHashCode() {
            return U.GetHashCode() ^ V.GetHashCode();
        }
    }
}