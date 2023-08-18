using System;
using UnityEngine;

[Serializable]
public class PidRegulation
{
    public float kp, ki, kd;
    private float _integral = 0;
    private float _dV = 0;
    private float _lastError = 0;
    
    public PidRegulation(float kp, float ki, float kd)
    {
        this.kp = kp;
        this.ki = ki;
        this.kd = kd;
    }
    
    public float GetPid(float error, float deltaTime)
    {
        _integral += error * deltaTime;
        _dV = (error - _lastError) / deltaTime;
        var pid = error * kp + _integral * ki + _dV * kd;
        _lastError = error;
        return pid;
    }
}