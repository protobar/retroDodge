using UnityEngine;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;

// ==================== BALL POOL ====================
public class BallPool : MonoBehaviour
{
    private static BallPool instance;
    public static BallPool Instance => instance;

    [Header("Pool Settings")]
    public GameObject ballPrefab;
    public int poolSize = 5;

    private Queue<BallController> ballPool = new Queue<BallController>();

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            InitializePool();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void InitializePool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject ballObj = Instantiate(ballPrefab);
            BallController ball = ballObj.GetComponent<BallController>();
            ballObj.SetActive(false);
            ballPool.Enqueue(ball);
        }
    }

    public BallController GetBall()
    {
        if (ballPool.Count > 0)
        {
            BallController ball = ballPool.Dequeue();
            ball.gameObject.SetActive(true);
            return ball;
        }
        else
        {
            // Create new ball if pool is empty
            GameObject ballObj = Instantiate(ballPrefab);
            return ballObj.GetComponent<BallController>();
        }
    }

    public void ReturnBall(BallController ball)
    {
        ball.gameObject.SetActive(false);
        ballPool.Enqueue(ball);
    }
}