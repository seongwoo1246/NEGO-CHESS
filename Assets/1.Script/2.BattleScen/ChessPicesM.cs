using System.Collections.Generic;
using UnityEngine;

public class ChessPieceM : MonoBehaviour
{
    [Header("참조 스크립트")]
    [SerializeField] private ChessBoardM boardManager; // 좌표 변환기 참조

    // 체스판 위에 존재하는 모든 기물 오브젝트를 논리 좌표(x, y)로 추적하기 위한 2차원 배열
    // (x: 0~7, y: 0~7) 위치에 존재하는 기물 GameObject를 저장합니다.
    private GameObject[,] pieceGrid = new GameObject[8, 8];

    /// <summary>
    /// 원하는 위치(x, y)에 원하는 기물 프리팹을 생성합니다.
    /// (변형 체스 특성에 맞춰 개수나 종류 제한 없이 호출 가능)
    /// </summary>
    /// <param name="piecePrefab">생성할 기물 프리팹</param>
    /// <param name="x">Target Grid X (0~7)</param>
    /// <param name="y">Target Grid Y (0~7)</param>
    /// <returns>생성된 기물의 GameObject</returns>
    public GameObject SpawnPiece(GameObject piecePrefab, int x, int y)
    {
        // 1. 이미 해당 위치에 기물이 있다면 기존 기물 제거 (또는 예외 처리)
        if (pieceGrid[x, y] != null)
        {
            DestroyPiece(x, y);
        }

        // 2. 입력받은 논리 좌표(x, y)를 기반으로 월드 좌표 산출
        Vector3 spawnWorldPos = boardManager.GetWorldPosition(x, y);

        // 3. 기물 인스턴스화
        // (주의: 추후 퓨전 멀티플레이 적용 시에는 Runner.Spawn()으로 대체됩니다)
        GameObject newPiece = Instantiate(piecePrefab, spawnWorldPos, Quaternion.identity);

        // 4. 2차원 배열에 기물 등록 (데이터 매핑)
        pieceGrid[x, y] = newPiece;

        return newPiece;
    }

    /// <summary>
    /// 지정한 출발 위치(fromX, fromY)의 기물을 목적지(toX, toY)로 이동시킵니다.
    /// </summary>
    /// <param name="fromX">출발 X (0~7)</param>
    /// <param name="fromY">출발 Y (0~7)</param>
    /// <param name="toX">도착 X (0~7)</param>
    /// <param name="toY">도착 Y (0~7)</param>
    public void MovePiece(int fromX, int fromY, int toX, int toY)
    {
        // 1. 출발지에 기물이 존재하는지 검증
        GameObject pieceToMove = pieceGrid[fromX, fromY];
        if (pieceToMove == null)
        {
            Debug.LogWarning($"[{fromX}, {fromY}] 위치에 이동시킬 기물이 없습니다!");
            return;
        }

        // 2. 도착지에 이미 상대 기물이 존재한다면 포획(잡기) 처리
        if (pieceGrid[toX, toY] != null)
        {
            DestroyPiece(toX, toY);
        }

        // 3. 목적지의 월드 좌표 계산
        Vector3 targetWorldPos = boardManager.GetWorldPosition(toX, toY);

        // 4. 실제로 기물의 Transform 위치 이동 (직부여 또는 연출용 트윈 적용)
        pieceToMove.transform.position = targetWorldPos;

        // 5. 논리 배열 데이터 업데이트 (출발지는 비우고, 목적지로 등록)
        pieceGrid[toX, toY] = pieceToMove;
        pieceGrid[fromX, fromY] = null;
    }

    /// <summary>
    /// 특정 위치의 기물을 제거(파괴/포획)합니다.
    /// </summary>
    public void DestroyPiece(int x, int y)
    {
        if (pieceGrid[x, y] != null)
        {
            Destroy(pieceGrid[x, y]);
            pieceGrid[x, y] = null;
        }
    }

    /// <summary>
    /// 특정 위치에 어떤 기물이 있는지 조회합니다.
    /// </summary>
    public GameObject GetPieceAt(int x, int y)
    {
        return pieceGrid[x, y];
    }
}