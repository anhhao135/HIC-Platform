using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QuickSort : MonoBehaviour
{

    public static void Quick_Sort<T>(T[] data, int left, int right) where T : IComparable<T>
    {
        int i, j;
        T pivot, temp;
        i = left;
        j = right;
        pivot = data[(left + right) / 2];

        do
        {
            while ((data[i].CompareTo(pivot) < 0) && (i < right)) i++;
            while ((pivot.CompareTo(data[j]) < 0) && (j > left)) j--;
            if (i <= j)
            {
                temp = data[i];
                data[i] = data[j];
                data[j] = temp;
                i++;
                j--;
            }
        } while (i <= j);

        if (left < j) Quick_Sort(data, left, j);
        if (i < right) Quick_Sort(data, i, right);
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
