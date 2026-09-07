using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Warlord.EditorTools.UI
{
    /// <summary>
    /// Заполнение приватных [SerializeField] через SerializedObject. Публиковать поля виджетов
    /// ради билдера нельзя — инспектор игрока не должен превращаться в свалку ссылок,
    /// поэтому связывание идёт тем же путём, каким его делает сам редактор.
    /// </summary>
    public sealed class Bind : IDisposable
    {
        private readonly SerializedObject _serialized;
        private readonly Object _target;

        public Bind(Object target)
        {
            _target = target;
            _serialized = new SerializedObject(target);
        }

        public Bind Ref(string field, Object value)
        {
            SerializedProperty property = Find(field);
            if (property != null)
                property.objectReferenceValue = value;

            return this;
        }

        public Bind Refs(string field, params Object[] values)
        {
            SerializedProperty property = Find(field);
            if (property == null)
                return this;

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

            return this;
        }

        public Bind Float(string field, float value)
        {
            SerializedProperty property = Find(field);
            if (property != null)
                property.floatValue = value;

            return this;
        }

        public Bind Int(string field, int value)
        {
            SerializedProperty property = Find(field);
            if (property != null)
                property.intValue = value;

            return this;
        }

        /// <summary>
        /// Значение перечисления. Отдельно от <see cref="Int"/> намеренно: у enum-свойства
        /// своё поле в SerializedProperty, и запись через intValue работает не для всех типов.
        /// </summary>
        public Bind Enum(string field, int value)
        {
            SerializedProperty property = Find(field);

            if (property == null)
                return this;

            if (property.propertyType == SerializedPropertyType.Enum)
                property.enumValueIndex = value;
            else
                property.intValue = value;

            return this;
        }

        public Bind Bool(string field, bool value)
        {
            SerializedProperty property = Find(field);
            if (property != null)
                property.boolValue = value;

            return this;
        }

        public Bind Tint(string field, Color value)
        {
            SerializedProperty property = Find(field);
            if (property != null)
                property.colorValue = value;

            return this;
        }

        public void Dispose() => _serialized.ApplyModifiedPropertiesWithoutUndo();

        private SerializedProperty Find(string field)
        {
            SerializedProperty property = _serialized.FindProperty(field);

            // Опечатка в имени поля иначе тихо оставит ссылку пустой, и виджет
            // молча перестанет работать уже в рантайме.
            if (property == null)
                Debug.LogError("Warlord UI: у " + _target.GetType().Name + " нет поля " + field);

            return property;
        }
    }
}
