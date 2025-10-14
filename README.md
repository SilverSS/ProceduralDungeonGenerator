# ProceduralDungeonGenerator

유니티 기반 절차적 던전 생성 프로젝트입니다. 2D/3D 던전 생성 알고리즘, 경로 탐색, 방/복도/계단 배치, 시각화 기능을 제공합니다.

## 각 클래스의 역할
- **Generator2D** : 2D 그리드 기반 절차적 던전 생성기
- **Generator3D** : 3D 볼륨 기반 절차적 던전 생성기
- **Grid2D/Grid3D** : 2D/3D 셀 상태를 관리하는 유틸리티 그리드
- **Delaunay2D/Delaunay3D** : 방 중심을 정점으로 들로네 삼각분할(2D)/테트라분할(3D) 수행
- **DungeonPathfinder2D/DungeonPathfinder3D** : A* 기반 경로 탐색 및 복도/계단 경로 생성
- **Room2D/Room3D** : 방의 위치, 크기, 속성, 셀 정보 관리
- **RoomVisualizer** : 방의 타입/속성에 따라 Gizmo 및 시각화 오브젝트 표시
- **CellProperties** : 각 셀의 외곽/입구/내부 여부 등 세부 정보 관리
- **DungeonGenerationOptions** : 적/아이템/상인/보스 출현 확률 등 던전 생성 옵션
- **Rotator** : 오브젝트를 Y축 기준으로 회전시키는 유틸리티

## 앞으로 추가 수정해야할 작업(TO-DO)

- **시각화 예시 프리팹 제작** : 던전 방/복도/계단/출입구/적/아이템 등 시각화용 프리팹 설계 및 제작 (진행중)
- **길 찾기 우선순위 로직 개선** : 휴리스틱 가중치 조정 필요
- **계단 및 방 입구 생성 로직 개선** : 계단을 최소화 할 수 있도록 하고 그에 따른 입구 위치 결정 하는 로직 구상 및 구현
- **프리팹 배치/테스트** : 제작한 프리팹을 Generator2D/Generator3D에 연결하고, 씬에서 배치 테스트
- **던전정보 저장 기능** : 던전 구조(방, 복도, 계단, 적, 아이템 등)를 ScriptableObject로 저장
- **던전 저장/불러오기 UI 구현** : 저장된 던전 정보를 불러오고, 새로 저장하는 UI 제작
- **던전 생성후 플레이어 캐릭터 테스트 플레이 기능** : 던전 생성 후 플레이어 캐릭터 자동 배치, 이동/충돌/입장/탈출 등 기본 플레이 테스트
- **플레이어/적 AI 기본 구현** : 플레이어 이동, 적 AI(순찰, 추적, 공격 등) 기본 동작 구현
- **테스트 플레이 자동화/리포트** : 던전 생성 후 자동 테스트 플레이 및 결과 리포트 저장

## 외부 라이브러리 및 BlueRaja 우선순위 큐
- 본 프로젝트의 경로 탐색(A* 알고리즘 등)에서는 속도를 위해 빠른 우선순위 큐를 사용했습니다.
- `Assets/BlueRaja` 폴더의 코드(FastPriorityQueue 등)는 DungeonPathfinder2D/DungeonPathfinder3D에서 open 리스트(우선순위 큐)로 사용됩니다.
- BlueRaja의 다양한 PriorityQueue 구현체를 활용(https://github.com/BlueRaja/High-Speed-Priority-Queue-for-C-Sharp)하여, 경로 탐색 시 최소 비용 노드를 빠르게 선택할 수 있습니다.