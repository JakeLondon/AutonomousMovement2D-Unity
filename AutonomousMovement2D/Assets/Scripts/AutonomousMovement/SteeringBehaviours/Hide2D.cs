﻿using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kensai.AutonomousMovement {
    public class Hide2D : SteeringBehaviour2D {
        public SteeringAgent2D Target = null;
        public float PanicDistance = -1; //TODO -> Decidir si va en config settings
        public float HidingDistanceFromObstacle = 1f;
        public float SlowingDistance = 1f;

        void Reset() {
            if (World2D.Instance != null) {
                Weight = World2D.Instance.DefaultSettings.HideWeight;
                Probability = World2D.Instance.DefaultSettings.HideProb;
                SlowingDistance = World2D.Instance.DefaultSettings.ArriveSlowingDistance;
                HidingDistanceFromObstacle = World2D.Instance.DefaultSettings.HidingDistanceFromObstacle;
            }
        }

        public override Vector2 GetVelocity() {
            return GetVelocity(agent, Target, HidingDistanceFromObstacle, SlowingDistance, PanicDistance);
        }

        public static Vector2 GetVelocity(SteeringAgent2D agent, SteeringAgent2D target, float hidingDistanceFromObstacle, float slowingDistance = 1f, float panicDistance = -1) {
            if (panicDistance > 0) {
                var sqrDistance = (agent.rigidbody2D.position - target.rigidbody2D.position).magnitude;
                if (sqrDistance > panicDistance) {
                    return Vector2.zero;
                }
            }

            float distToClosest = float.MaxValue;
            Vector2 bestHidingSpot = Vector2.zero;

            foreach (var obstacle in World2D.Instance.Obstacles) {
                var hidingSpot = GetHidingPosition(obstacle.transform.position, obstacle.radius, target.rigidbody2D.position, hidingDistanceFromObstacle);
                var dist = (hidingSpot - agent.rigidbody2D.position).magnitude;
                if (dist < distToClosest) {
                    distToClosest = dist;
                    bestHidingSpot = hidingSpot;
                }
            }

            if (distToClosest == float.MaxValue) {
                return Evade2D.GetVelocity(agent, target);
            }

            return Arrive2D.GetVelocity(agent, bestHidingSpot, slowingDistance);
        }

        public static Vector2 GetHidingPosition(Vector2 obstaclePosition, float obstacleRadius, Vector2 targetPosition, float distanceFromCover) {
            float distAway = obstacleRadius + distanceFromCover;
            Vector2 toObstacle = (obstaclePosition - targetPosition).normalized;
            return (toObstacle * distAway) + obstaclePosition;
        }
    }
}
