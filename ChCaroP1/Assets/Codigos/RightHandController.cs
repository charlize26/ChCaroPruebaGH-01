using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

public class RightHandController : MonoBehaviour
{
    //estado
    [SerializeField] private XRRayInteractor _XRRayInteractor_Grab;
    [SerializeField] private XRRayInteractor _XRRayInteractor_Teleport;

    [SerializeField] private InputActionReference _Joystick_North_Sector;
    private void Awake()
    {
        _XRRayInteractor_Teleport.enabled = false;
    }

    private void OnEnable()
    {
        _Joystick_North_Sector.action.performed += PalancaArribaPresionada; //ejecutrar la acción palanca arriba
        _Joystick_North_Sector.action.canceled += PalancaArribaPLiberada; //soltar la acción palanca arriba
    }
    private void OnDisable()
    {
        _Joystick_North_Sector.action.performed -= PalancaArribaPresionada;
        _Joystick_North_Sector.action.canceled -= PalancaArribaPLiberada;
    }

    private void PalancaArribaPresionada(InputAction.CallbackContext context)
    {
        _XRRayInteractor_Grab.enabled = false;
        _XRRayInteractor_Teleport.enabled = true;
    }

    //Lamda expression
    private void PalancaArribaPLiberada(InputAction.CallbackContext context) => Invoke("PalancaArribaPLiberada_Invoke", 0.01f);


    private void PalancaArribaPLiberada_Invoke()
    {
        _XRRayInteractor_Grab.enabled = true;
        _XRRayInteractor_Teleport.enabled = false;
    }



}

