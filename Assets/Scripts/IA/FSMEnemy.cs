﻿using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

public class FSMEnemy : MonoBehaviour {

    // point that enemy have to reach
    public Transform[] points;
    // radious of overlap circle
    public float overlapRadius;
    // patrol time
    [Range(0.1f, 5f)] public float FSMDelay;
    // time to wait before move to the next point
    [Range(1f, 10f)] public float idleTime;
    // time to wait before move to the next point
    [Range(0.1f, 1f)] public float maxDistanceUntilPoint;
    // spped of enemy
    public float speed;
    // point mask
    public LayerMask pointsMask;
    // wall mask
    public LayerMask obstaclesMask;
    // interactable mask
    public LayerMask interactableMask;

    FSM fsmMachine;
    // next position
    int next;
    Transform nextPosition;
    Transform throwablePosition;
    // used to came back to the first known point
    Queue<Transform> toThrowableAndBack = new Queue<Transform>();
    // check if player is chought
    bool isEnd;
    bool isIdle;
    bool isMoving;
    bool isPositionReached;
    bool isReachingThrowable;
    bool isCameBackToIdle;
    bool throwableIsReached;
    // check if player is found
    bool isPlayer;

    void Start() {
        StartFSM();
    }

    void Update() {
        if (isMoving || isReachingThrowable) {
            Moving();
        }
    }

    // move the player
    void Moving() {
        // move player to next position if distance from element is less then the default
        if (maxDistanceUntilPoint > Vector2.Distance(transform.position, nextPosition.position)) {
            // move
            transform.position = Vector2.MoveTowards(transform.position, nextPosition.position, speed * Time.deltaTime);
        }
        else {
            // stop moving and came back to idle
            if (!isIdle) {
                isMoving = false;
                isPositionReached = true;
            }
            else {
                // you are arrived
                if (nextPosition == throwablePosition) {
                    isReachingThrowable = false;
                    throwablePosition = null;
                    throwableIsReached = true;
                }
                else {
                    // add current position before search the next
                    toThrowableAndBack.Enqueue(nextPosition);
                    // find the next position near the throwable
                    FindPosition();
                }
            }
        }
    }

    public void StartFSM() {

        // Define states and link actions when enter/exit/stay
        FSMState idleAction = new FSMState {
            enterActions = new FSMAction[] { WaitBeforeMove }
        };

        FSMState moveAction = new FSMState {
            enterActions = new FSMAction[] { Move }
        };

        FSMState seekAction = new FSMState {
            enterActions = new FSMAction[] { FindPosition }
        };

        FSMState catchAction = new FSMState {
            enterActions = new FSMAction[] { EndLevel }
        };

        // Define transitions
        FSMTransition fromIdleToMove = new FSMTransition(CheckTimeToMove);
        FSMTransition fromMoveToIdle = new FSMTransition(CheckRechedPosition);
        FSMTransition fromIdleToCatch = new FSMTransition(CheckPlayer);
        FSMTransition fromIdleToSeek = new FSMTransition(CheckNearObject);
        FSMTransition fromSeekToIdle = new FSMTransition(CheckReachedObject);
        FSMTransition fromSeekToCatch = new FSMTransition(CheckPlayer);
        FSMTransition fromMoveToSeek = new FSMTransition(CheckNearObject);
        FSMTransition fromMoveToCatch = new FSMTransition(CheckPlayer);

        // Link states with transitions
        idleAction.AddTransition(fromIdleToMove, moveAction);
        idleAction.AddTransition(fromIdleToCatch, catchAction);
        idleAction.AddTransition(fromIdleToSeek, seekAction);
        moveAction.AddTransition(fromMoveToIdle, idleAction);
        moveAction.AddTransition(fromMoveToSeek, seekAction);
        moveAction.AddTransition(fromMoveToCatch, catchAction);
        seekAction.AddTransition(fromSeekToIdle, idleAction);
        seekAction.AddTransition(fromSeekToCatch, catchAction);

        // Setup a FSA at initial state
        fsmMachine = new FSM(idleAction);
        // Start monitoring
        StartCoroutine(PatrolFSM());
    }

    // wait seconds before move
    void WaitBeforeMove() {
        StartCoroutine("WaitToMove");
    }

    IEnumerator WaitToMove() {
        yield return null;
        // if is not already moving
        if (isIdle) {
            yield return new WaitForSeconds(idleTime);
            // is ready to move to next point
            isIdle = false;
        }
    }

    // move the player to next position
    void Move() {
        // it is came back to idle
        if (!isCameBackToIdle) {
            // if it can't move thoward a door
            if (!CheckDoor()) {
                // reverse array and came back
                next = points.Length - next;
                Array.Reverse(points);
            }
            next++;
            nextPosition = points[next];
            isMoving = true;
            // if we re at the end of the array
            if (next + 1 == points.Length) {
                // reverse array
                Array.Reverse(points);
                next = 0;
            }
        }
        else {
            isCameBackToIdle = false;
        }
    }

    // check if door is open or close
    bool CheckDoor() {
        // if the door is closed
        if (Physics2D.Raycast(transform.position, nextPosition.position, Vector2.Distance(transform.position, nextPosition.position), interactableMask)) {
            return false;
        }
        return true;
    }

    // find position to be reached
    void FindPosition() {
        Transform minBetweenThrowableAndPoints = GetMinBetweenThrowableAndPoints();
        // if there are no point the nearest is the throwable itself
        if (minBetweenThrowableAndPoints == null) {
            nextPosition = throwablePosition;
        }
        else {
            // // get all the point near throwable
            Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, overlapRadius, pointsMask);
            Transform min = null;
            for (int i = 0; i < colliders.Length; i++) {
                // check if a wall is not between the point and the enemy
                if (!Physics2D.Raycast(transform.position, colliders[i].transform.position, Vector2.Distance(throwablePosition.position, colliders[i].transform.position), obstaclesMask)) {
                    // get the min
                    if (min == null || (Vector2.Distance(minBetweenThrowableAndPoints.position, min.position) > Vector2.Distance(minBetweenThrowableAndPoints.position, colliders[i].transform.position))) {
                        min = colliders[i].transform;
                    }
                }
            }
            nextPosition = min;
        }
        isReachingThrowable = true;
    }

    Transform GetMinBetweenThrowableAndPoints() {
        // get all the point near throwable
        Collider2D[] colliders = Physics2D.OverlapCircleAll(throwablePosition.position, overlapRadius, pointsMask);
        Transform min = null;
        for (int i = 0; i < colliders.Length; i++) {
            // check if a wall is not between the point and the throwable
            if (!Physics2D.Raycast(throwablePosition.position, colliders[i].transform.position, Vector2.Distance(throwablePosition.position, colliders[i].transform.position), obstaclesMask)) {
                // get the min
                if (min == null || (Vector2.Distance(throwablePosition.position, min.position) > Vector2.Distance(throwablePosition.position, colliders[i].transform.position))) {
                    min = colliders[i].transform;
                }
            }
        }
        return min;
    }

    // game over
    void EndLevel() {
        // TODO: closs scene
        isEnd = true;
    }

    // Periodic update, run forever
    IEnumerator PatrolFSM() {
        while (!isEnd) {
            fsmMachine.Update();
            yield return new WaitForSeconds(FSMDelay);
        }
    }

    // check if is time to switch to move
    bool CheckTimeToMove() {
        if (!isIdle) {
            return true;
        }
        return false;
    }

    // check if is time to switch to idle
    bool CheckRechedPosition() {
        if (isPositionReached) {
            isPositionReached = false;
            // idle until next time to move
            isIdle = true;
            return true;
        }
        return false;
    }

    // check if is time to switch to gameover
    bool CheckPlayer() {
        if (isPlayer) {
            return true;
        }
        return false;
    }

    // check if is time to reach to seek
    bool CheckNearObject() {
        if (throwablePosition != null) {
            return true;
        }
        return false;
    }

    // check if is time to came back to idle
    bool CheckReachedObject() {
        if (throwableIsReached) {
            throwableIsReached = false;
            isCameBackToIdle = true;
            return true;
        }
        return false;
    }

    // check enter collision
    void OnCollisionEnter(Collision collision) {
        if (collision.transform.tag == "Player") {
            // TODO: check just if player is in front direction
            isPlayer = true;
        }
        if (collision.transform.tag == "Throwable") {
            throwablePosition = collision.transform;
        }
    }
}
