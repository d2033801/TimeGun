using UnityEngine;
using Utility;

namespace TEST
{
    public class Test : MonoBehaviour
    {
        RingBuffer<int> ringBuffer = new(5);

        private SimpleTimeRewind o;
        private SimpleTimeRewind o2;
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            o = transform.GetChild(0).GetComponent<SimpleTimeRewind>();
            o2 = transform.GetChild(1).GetComponent<SimpleTimeRewind>();
            ringBuffer.Push(1);
            ringBuffer.Push(2);
            ringBuffer.Push(5);
            ringBuffer.Push(100);
            ringBuffer.Push(1000);
            ringBuffer.Push(10000);
            foreach (var item in ringBuffer)
            {
                Debug.Log(item);
            }

        }

        // Update is called once per frame
        void Update()
        {
            o.transform.Translate(Vector3.right * 1f * Time.deltaTime);
            o.transform.Rotate(Vector3.right * 30f * Time.deltaTime);
        }

        public void StartRecord()
        {
            o.StartRecord();
            o2.StartRecord();
        }

        public void StopRecord()
        {
            o.StopRecord();
            o2.StopRecord();
        }

        public void StartRewind()
        {
            o.StartRewind();
            o2.StartRewind();
        }

        public void Rewind3Sec()
        {
            o.RewindBySeconds(3);
            o2.RewindBySeconds(3);
        }


        public void StopRewind()
        {
            o.StopRewind();
            o2.StopRewind();
        }
    }

}
