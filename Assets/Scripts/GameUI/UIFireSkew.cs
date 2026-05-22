using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.GameUI
{
    [RequireComponent(typeof(Graphic))]
    public class UIFireSkew : BaseMeshEffect
    {
        [Header("Skew Settings")]
        [Tooltip("How far the top of the sprite sways left and right.")]
        public float skewAmount = 15f;
        
        [Tooltip("How fast the sway animation runs.")]
        public float speed = 10f;
        
        [Tooltip("Add some random offset so multiple fires don't sync up perfectly if you have more than one. (Set >0 to enable)")]
        public float randomPhase = 3f;

        private Graphic m_Graphic;
        private float m_TimeOffset;

        protected override void Awake()
        {
            base.Awake();
            m_Graphic = GetComponent<Graphic>();
            m_TimeOffset = Random.Range(0f, randomPhase);
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;

            // Step 1: Find the vertical boundaries to calculate normalized height
            float minY = float.MaxValue;
            float maxY = float.MinValue;

            UIVertex vertex = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                if (vertex.position.y < minY) minY = vertex.position.y;
                if (vertex.position.y > maxY) maxY = vertex.position.y;
            }

            float height = maxY - minY;
            if (height <= 0) return;

            // Step 2: Calculate the horizontal offset using a Sine wave based on Time
            float sway = Mathf.Sin((Time.time + m_TimeOffset) * speed) * skewAmount;
            
            // Step 3: Apply the offset proportionally based on height. 
            // Bottom (normalizedY = 0) won't move, Top (normalizedY = 1) will move by full 'sway' amount.
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                
                float normalizedY = (vertex.position.y - minY) / height;
                vertex.position.x += sway * normalizedY;
                
                vh.SetUIVertex(vertex, i);
            }
        }
        
        private void Update()
        {
            // Rebuild mesh at half rate — sway is slow enough to stay visually identical.
            if (m_Graphic != null && (Time.frameCount & 1) == 0)
            {
                m_Graphic.SetVerticesDirty();
            }
        }
    }
}
