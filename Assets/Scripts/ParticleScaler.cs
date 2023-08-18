
using UnityEngine;
using System.Collections.Generic;

public class ParticleScaler : MonoBehaviour
{
    public Dictionary<ParticleSystem, float> ParticleSystemsOriginalSizes = new Dictionary<ParticleSystem, float>();
    public ParticleSystem[] ParticleSystems;
    [ContextMenu("Get particle systems from children")]
    void GetParticleSystems()
    {
        ParticleSystems = GetComponentsInChildren<ParticleSystem>();
        //Set particle systems original sizes
        ParticleSystemsOriginalSizes.Clear();
        for (int i = 0; i < ParticleSystems.Length; i++)
        {
            ParticleSystemsOriginalSizes.Add(ParticleSystems[i], ParticleSystems[i].startSize);
        }
    }
    [ContextMenu("Change particle systems size")]
    void ChangeParticleSystemsSize()
    {
        if (ParticleSystems == null)
        {
            Debug.LogError("ParticleSystems is null");
            return;
        }
        for (int i = 0; i < ParticleSystems.Length; i++)
        {
            ParticleSystems[i].startSize = ParticleSystemsOriginalSizes[ParticleSystems[i]] *
                                           new Vector2(transform.localScale.x, transform.localScale.y).magnitude;
        }
    }
    [ContextMenu("Restore particle systems size")]
    void RestoreParticleSystemsSize()
    {
        if (ParticleSystems == null)
        {
            Debug.LogError("ParticleSystems is null");
            return;
        }
        for (int i = 0; i < ParticleSystems.Length; i++)
        {
            ParticleSystems[i].startSize = ParticleSystemsOriginalSizes[ParticleSystems[i]];
        }
        transform.localScale = Vector3.one;
    }
    
    private void Start()
    {
        GetParticleSystems();
        ChangeParticleSystemsSize();
    }
    
  
}