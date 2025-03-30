using System.Collections.Generic;
using UnityEngine;

public class ComposeSquatControll : MonoBehaviour
{
    [Header("Settings")] 
    [SerializeField] private int widthPointsCount;
    [SerializeField] private Vector2 pointDistanceSize;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        List<List<Vector3>> points = CalculateTriangle(transform, widthPointsCount, pointDistanceSize);
        for (int i = 0; i < points.Count; i++)
        {
            for (int j = 0; j < points[i].Count; j++)
            {
                Gizmos.DrawSphere(points[i][j], 0.6f);
            }
        }
    }

    private List<List<Vector3>> CalculateTriangle(Transform cornest, int width, Vector2 size)
    {
        List<List<Vector3>> result = new List<List<Vector3>>();
        int h = 0;
        
        while (width > 0)
        {
            result.Add(new());
            float startXOffset = ((width % 2 == 0 ? width / 2 : (width - 1) / 2) * size.x);
            if (width % 2 == 0)
                startXOffset -= size.x / 2;

            Vector3 right = cornest.right;
            Vector3 back = -cornest.forward;
            Vector3 startPos = cornest.position + (right * startXOffset * -1);
            int middleIndex = width / 2;
            
            for (int i = 0; i < width; i++)
            {
                if (width % 2 == 1 && i == middleIndex && h == 0) continue; 
                    
                Vector3 point = startPos + (right * i * size.x);
                point += back * size.y * h;
                
                result[^1].Add(point);
            }

            width -= width - 2 > 0 ? 2 : 1;
            h++;
        }

        return result;
    }
}

