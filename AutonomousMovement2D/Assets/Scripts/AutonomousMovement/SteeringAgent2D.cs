﻿using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kensai.Util.Extensions;

namespace Kensai.AutonomousMovement {
    [RequireComponent(typeof(Rigidbody2D))]
    public class SteeringAgent2D : MonoBehaviour {
        private List<SteeringBehaviour2D> steeringBehaviours = new List<SteeringBehaviour2D>();
        public List<SteeringBehaviour2D> SteeringBehaviours {
            get { return steeringBehaviours; }
            set { steeringBehaviours = value; }
        }

        private Vector2 heading = Vector2.up;
        public Vector2 Heading {
            get { return heading; }
            private set { heading = value; }
        }

        private List<SteeringAgent2D> neighbors = new List<SteeringAgent2D>();
        /// <summary>
        /// Contains other Steering Agents that are close to the current one, as defined by NeighborRadius
        /// </summary>
        public List<SteeringAgent2D> Neighbors {
            get { return neighbors; }
            set { neighbors = value; }
        }

        public float MaxSpeed = 5;
        public float MaxForce = 3;
        public float Radius = 1;
        public float NeighborRadius = 3;
        public bool DrawGizmos = false;

        private Vector2 steeringForce;
        private Smoother<Vector2> headingSmoother;
        private int behavioursRequiringNeighbors = 0;
        private Vector2 previousPosition;

        void Reset() {
            NeighborRadius = World2D.Instance.DefaultSettings.DefaultNeighborRadius;
        }

        void Awake() {
            var circleCollider = GetComponent<CircleCollider2D>();

            //TODO -> Revise how scale affects these things
            if (circleCollider != null) {
                Radius = circleCollider.radius;
            } else if (collider2D != null) {
                Radius = Mathf.Max(collider2D.bounds.extents.x, collider2D.bounds.extents.y);
            } else if (renderer != null) {
                Radius = Mathf.Max(renderer.bounds.extents.x, renderer.bounds.extents.y);
            }
        }

        void Start() {
            if (World2D.Instance == null) { 
                throw new Exception("There is no instance of World2D. You must attach a World2D script to the scene."); 
            }

            if (World2D.Instance.DefaultSettings.SmoothHeadingOn) {
                headingSmoother = new Smoother<Vector2>(World2D.Instance.DefaultSettings.NumSamplesForSmoothing);
            }

            if (World2D.Instance.SpacePartition != null) {
                World2D.Instance.SpacePartition.AddEntity(this);
            }

            previousPosition = this.rigidbody2D.position;

            World2D.Instance.AgentList.Add(this);
        }

        void FixedUpdate() {

            if (World2D.Instance != null && World2D.Instance.wrapAround) { 
                WrapAround(gameObject.rigidbody2D.position, World2D.Instance.worldSizeX, World2D.Instance.worldSizeY);
            }

            if (World2D.Instance.SpacePartition != null) {
                World2D.Instance.SpacePartition.UpdateEntity(this, previousPosition);
            }

            if (behavioursRequiringNeighbors > 0) {
                Neighbors = GetNeighbors();
            }
            var steeringForce = Vector2.zero;
            steeringForce += SteeringBehaviours.CalculateCompound(MaxForce, SteeringBehaviourExtensions.SteeringCombinationType.PrioritizedDithering);

            rigidbody2D.AddForce(steeringForce, ForceMode2D.Impulse);
            rigidbody2D.velocity = rigidbody2D.velocity.Truncate(MaxSpeed);

            if (rigidbody2D.velocity.sqrMagnitude > 0.000001) {
                Heading = rigidbody2D.velocity.normalized;

                if (World2D.Instance.DefaultSettings.SmoothHeadingOn) {
                    Heading = headingSmoother.Update(Heading).normalized;
                }
            }

            transform.up = Heading;
            previousPosition = rigidbody2D.position;
        }

        void OnDrawGizmos() {
            if (DrawGizmos) {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(rigidbody2D.position, rigidbody2D.velocity + rigidbody2D.position);
                Gizmos.color = Color.white;
                Gizmos.DrawLine(rigidbody2D.position, steeringForce + rigidbody2D.position);
                Gizmos.DrawWireSphere(rigidbody2D.position, NeighborRadius);
            }
        }

        void OnGUI() {
            
        }

        void OnDestroy() {
            if (World2D.Instance != null) { 
                World2D.Instance.AgentList.Remove(this);
            }
        }

        public void RegisterSteeringBehaviour(SteeringBehaviour2D behaviour) {
            SteeringBehaviours.Add(behaviour);
            if (behaviour.RequiresNeighborList) behavioursRequiringNeighbors++;
        }

        public void DeregisterSteeringBehaviour(SteeringBehaviour2D behaviour) {
            SteeringBehaviours.Add(behaviour);
            if (behaviour.RequiresNeighborList) behavioursRequiringNeighbors--;
        }

        private void WrapAround(Vector2 position, float xWorldSize, float yWorldSize) {
            float x = position.x, y = position.y;
            if (rigidbody2D.position.x > xWorldSize) x = 0.0f;
            if (rigidbody2D.position.x < 0) x = xWorldSize;
            if (rigidbody2D.position.y > yWorldSize) y = 0.0f;
            if (rigidbody2D.position.y < 0) y = yWorldSize;
            rigidbody2D.position = new Vector2(x, y);
        }

        private List<SteeringAgent2D> GetNeighbors() {
            var neighbors = new List<SteeringAgent2D>();

            if (World2D.Instance.SpacePartition == null) { 
                foreach (var agent in World2D.Instance.AgentList) {
                    if (agent == this) continue;

                    var distance = (agent.rigidbody2D.position - rigidbody2D.position).magnitude + Radius + agent.Radius;
                    if (distance <= NeighborRadius) {
                        neighbors.Add(agent);
                    }
                }
            } else {
                var spacePartition = World2D.Instance.SpacePartition;
                var testRect = new Rect(rigidbody2D.transform.position.x - NeighborRadius,
                                        rigidbody2D.transform.position.y - NeighborRadius,
                                        rigidbody2D.transform.position.x + NeighborRadius,
                                        rigidbody2D.transform.position.y + NeighborRadius);
                foreach (var cell in spacePartition.Cells) {
                    if (cell.Rect.Overlaps(testRect)) {
                        for (int i = 0; i < cell.Members.Count; i++) {
                            if (cell.Members[i] == this) continue;
                            var distance = (cell.Members[i].rigidbody2D.position - rigidbody2D.position).magnitude + Radius + cell.Members[i].Radius;
                            if (distance <= NeighborRadius) {
                                neighbors.Add(cell.Members[i]);
                            }
                        }
                    }
                }
            }

            return neighbors;
        }
    }
}
