using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AnimationPlayer.Internal
{
    public class TestSetValue
    {
        private DataContainer container;
        private SerializedObject containerSO;

        public class Data1Container : ValueContainer<Data1> { }
        public class Data2Container : ValueContainer<Data2> { }
        public class Data3Container : ValueContainer<Data3> { }

        [Serializable]
        public class Data1
        {
            public int i;
        }

        [Serializable]
        public class Data2
        {
            public int i;
            public string s;
            public float f;
        }

        [Serializable]
        public class Data3
        {
            public Data1 data1;
            public float f;
            public Data2 data2;
        }

        public class DataContainer : ScriptableObject
        {
            public int justAnInt;
            public Data1 data1;public Data2 data2;
            public Data3 data3;
        }

        [SetUp]
        public void SetUp() {
            container = ScriptableObject.CreateInstance<DataContainer>();
            containerSO = new SerializedObject(container);
            
        }

        [Test]
        public void SmokeTest() {
            container.justAnInt = 123;
            containerSO.FindProperty(nameof(DataContainer.justAnInt)).intValue = 3051;
            Assert.AreEqual(123, container.justAnInt);
            containerSO.ApplyModifiedProperties();
            Assert.AreEqual(3051, container.justAnInt);
        }

        [Test]
        public void TestValueContainer() {
            var valueContainer = ScriptableObject.CreateInstance<IntContainer>();
            Assert.NotNull(valueContainer);
            valueContainer.t = 5029;
            var value = new SerializedObject(valueContainer);
            Assert.AreEqual(5029, value.FindProperty(nameof(IntContainer.t)).intValue);
        }

        [Test]
        public void TestJustAnInt()
        {
            var justAnIntProp = containerSO.FindProperty(nameof(DataContainer.justAnInt));
            SerializedPropertyHelper.SetValue(justAnIntProp, 13);

            Assert.AreEqual(13, container.justAnInt);
        }

        [Test]
        public void TestSetData1Value() {
            var data = new Data1 { i = 15 };
            var dataProp = containerSO.FindProperty(nameof(DataContainer.data1));

            SerializedPropertyHelper.SetValue(dataProp, data);

            Assert.AreEqual(15, container.data1.i);
        }

        [Test]
        public void TestSetData2Value()
        {
            var data2 = new Data2
            {
                i = 2345,
                s = "1dkløasgjp+",
                f = 58582f
            };
            var dataProp = containerSO.FindProperty(nameof(DataContainer.data2));
            SerializedPropertyHelper.SetValue(dataProp, data2);
            
            Assert.AreEqual(data2.i, container.data2.i);
            Assert.AreEqual(data2.s, container.data2.s);
            Assert.AreEqual(data2.f, container.data2.f);
        }

        [Test]
        public void TestCompound()
        {
            var data3 = new Data3
            {
                data1 = new Data1 { i = 890 },
                f = -2689789f,
                data2 = new Data2
                {
                    f = 7892f,
                    i = -25742,
                    s = "øøøøøøøøøøøø"
                }
            };
            var dataProp = containerSO.FindProperty(nameof(DataContainer.data3));
            SerializedPropertyHelper.SetValue(dataProp, data3);
            
            Assert.AreEqual(data3.data1.i, container.data3.data1.i);
            Assert.AreEqual(data3.f, container.data3.f);
            Assert.AreEqual(data3.data2.f, container.data3.data2.f);
            Assert.AreEqual(data3.data2.i, container.data3.data2.i);
            Assert.AreEqual(data3.data2.s, container.data3.data2.s);
        }
    }
}