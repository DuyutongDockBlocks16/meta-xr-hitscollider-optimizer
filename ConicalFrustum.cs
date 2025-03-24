/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * Licensed under the Oculus SDK License Agreement (the "License");
 * you may not use the Oculus SDK except in compliance with the License,
 * which is provided at the time of installation or download, or which
 * otherwise accompanies this software in either electronic or hard copy form.
 *
 * You may obtain a copy of the License at
 *
 * https://developer.oculus.com/licenses/oculussdk/
 *
 * Unless required by applicable law or agreed to in writing, the Oculus SDK
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using UnityEditor;
using UnityEngine;

namespace Oculus.Interaction
{
    public class ConicalFrustum : MonoBehaviour
    {
        [SerializeField]
        [Min(0f)]
        private float _minLength = 0f;

        [SerializeField]
        [Min(0f)]
        private float _maxLength = 10f;

        [SerializeField]
        [Min(0f)]
        private float _radiusStart = 0.03f;

        [SerializeField]
        [Range(0f, 90f)]
        private float _apertureDegrees = 20f;

        public Pose Pose => this.transform.GetPose();

        #region public properties
        public float MinLength
        {
            get
            {
                return _minLength;
            }
            set
            {
                _minLength = value;
            }
        }

        public float MaxLength
        {
            get
            {
                return _maxLength;
            }
            set
            {
                _maxLength = value;
            }
        }

        public float RadiusStart
        {
            get
            {
                return _radiusStart;
            }
            set
            {
                _radiusStart = value;
            }
        }

        public float ApertureDegrees
        {
            get
            {
                return _apertureDegrees;
            }
            set
            {
                _apertureDegrees = value;
            }
        }

        public Vector3 StartPoint
        {
            get
            {
                return this.transform.position + Direction * MinLength;
            }
        }

        public Vector3 EndPoint
        {
            get
            {
                return this.transform.position + Direction * MaxLength;
            }
        }

        public Vector3 Direction
        {
            get
            {
                return this.transform.forward;
            }
        }
        #endregion

        public bool IsPointInConeFrustum(Vector3 point)
        {
            Vector3 coneOriginToPoint = point - this.transform.position;
            Vector3 pointProjection = Vector3.Project(coneOriginToPoint, Direction);
            if(Vector3.Dot(pointProjection, Direction) < 0)
            {
                return false;
            }
            float pointLength = pointProjection.magnitude;

            if (pointLength < _minLength
                || pointLength > _maxLength)
            {
                return false;
            }

            float pointRadius = Vector3.Distance(Pose.position + pointProjection, point);
            return pointRadius <= ConeFrustumRadiusAtLength(pointLength);
        }

        public float ConeFrustumRadiusAtLength(float length)
        {
            float radiusEnd = _maxLength * Mathf.Tan(_apertureDegrees * Mathf.Deg2Rad);

            float lengthRatio = length / _maxLength;
            float radiusAtLength = Mathf.Lerp(_radiusStart, radiusEnd, lengthRatio);
            return radiusAtLength;
        }

        public bool HitsCollider(Collider collider, out float score, out Vector3 point)
        {
            Vector3 centerPosition = collider.bounds.center;
            Vector3 projectedCenter = Pose.position
                + Vector3.Project(centerPosition - Pose.position, Pose.forward);

            point = collider.ClosestPointOnBounds(projectedCenter);

            if (!IsPointInConeFrustum(point))
            {
                score = 0f;
                return false;
            }

            Vector3 originToInteractable = point - Pose.position;
            float angleToInteractable = Vector3.Angle(originToInteractable.normalized, Pose.forward);
            score = 1f - Mathf.Clamp01(angleToInteractable / _apertureDegrees);
            return true;
        }

        public Vector3 getForward()
        {
            return Pose.forward;
        }


        public Vector3 getPosition()
        {
            return Pose.position;
        }
            

        public bool HitsColliderGaussianPureHand(Collider collider, out float score, out Vector3 point)
        {   
            // get hand's(wrist) position and forward
            Vector3 handPosition = Pose.position;
            Vector3 handDirection = Pose.forward;
            // int circleResolution = 8;
            float sigma = 0.4f;

            // get obect center
            Vector3 centerPosition = collider.bounds.center;

            // calculate the intersection of hand and plane
            point = CalculateIntersection(handPosition, handDirection, centerPosition);

            // DrawPoint(point);

            score = CalculateGaussianProbability(point, centerPosition, sigma);

            if (score == 0)
            {
                return false;
            }

            return true;
        }

        public bool HitsColliderGaussianHeadHand(Vector3 headPosition, Collider collider, out float score, out Vector3 point)
        {   
            // get hand's(wrist) position and forward
            Vector3 handPosition = Pose.position;
            Vector3 headHandDirection = handPosition - headPosition;
            Vector3 normalizedHeadHandDirection = headHandDirection.normalized;

            // int circleResolution = 8;
            float sigma = 0.4f;

            // get object center
            Vector3 centerPosition = collider.bounds.center;

            // calculate the intersection of hand and plane
            point = CalculateIntersection(handPosition, normalizedHeadHandDirection, centerPosition);

            // DrawPoint(point);

            score = CalculateGaussianProbability(point, centerPosition, sigma);

            if (score == 0)
            {
                return false;
            }

            return true;
        }

        public bool HitsColliderGaussianBodyHand(Vector3 bodyPosition, Collider collider, out float score, out Vector3 point)
        {   
            // get hand's(wrist) position and forward
            Vector3 handPosition = Pose.position;
            Vector3 bodyHandDirection = handPosition - bodyPosition;
            Vector3 normalizedBodyHandDirection = bodyHandDirection.normalized;

            float sigma = 0.4f;

            Vector3 centerPosition = collider.bounds.center;

            point = CalculateIntersection(handPosition, normalizedBodyHandDirection, centerPosition);

            // DrawPoint(point);

            score = CalculateGaussianProbability(point, centerPosition, sigma);

            if (score == 0)
            {
                return false;
            }

            return true;
        }


        private Vector3 CalculateIntersection(Vector3 handPosition, Vector3 handDirection, Vector3 centerPosition)
        {
            float planeY = centerPosition.y;


            float lambda = (planeY - handPosition.y) / handDirection.y;
            float x = handPosition.x + lambda * handDirection.x;
            float z = handPosition.z + lambda * handDirection.z;
            float y = centerPosition.y;

            return new Vector3(x, planeY, z); // return intersection
        }

        private float CalculateGaussianProbability(Vector3 point, Vector3 mean, float sigma)
        {
            float exponent = -0.5f * Mathf.Pow(Vector3.Distance(point, mean) / sigma, 2);
            return (1.0f / (Mathf.Sqrt(2 * Mathf.PI) * sigma)) * Mathf.Exp(exponent);
        }

        private void DrawPoint(Vector3 point)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Renderer renderer = sphere.GetComponent<MeshRenderer>();
            renderer.material.color = Color.red;
            sphere.transform.position = point;
            sphere.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f); 
            Destroy(sphere, 0.3f);
        }

        private void DrawGaussianCircle(Vector3 center, int circleResolution, float sigma)
        {
            float step = 2 * Mathf.PI / circleResolution;

            for (int i = 0; i < circleResolution; i++)
            {
                float angle = i * step;

                Vector3 spherePosition = center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * sigma;

                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.position = spherePosition;
                sphere.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f); 
            }
        }



        public Vector3 NearestColliderHit(Collider collider, out float score)
        {
            Vector3 centerPosition = collider.bounds.center;
            Vector3 projectedCenter = Pose.position
                + Vector3.Project(centerPosition - Pose.position, Pose.forward);
            Vector3 point = collider.ClosestPointOnBounds(projectedCenter);

            Vector3 originToInteractable = point - Pose.position;
            float vectorAngle = Vector3.Angle(originToInteractable.normalized, Pose.forward);
            score = 1f - Mathf.Clamp01(vectorAngle / _apertureDegrees);

            return point;
        }
    }
}
