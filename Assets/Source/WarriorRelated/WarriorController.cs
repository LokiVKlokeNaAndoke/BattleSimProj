﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Warrior
{
    public class WarriorController : MonoBehaviour
    {
        private SpriteRenderer sprRend;

        public const float distanceOfAttack = 1f;
        public float speed;
        public float rotationSpeed;
        public NeuralAI ai;
        public int team;

        public Actions.Action currentAction;

        private float bloodRemaining;
        private float bloodMax;

        public APlayerController PlayerOwner { get { return playerOwner; } }
        private APlayerController playerOwner;

        private void Start()
        {
            currentAction = new Actions.IdleAction();
            sprRend = GetComponent<SpriteRenderer>();
            //sprRend.color = team == 1 ? Color.blue : Color.red;
        }

        public void Initialize(Vector2 pos, StructureOfWarrior str, int team, NeuralAI ai, APlayerController pla)
        {
            transform.position = pos;

            playerOwner = pla;

            this.ai = ai;
            this.team = team;
            this.bloodRemaining = str.blood;
            bloodMax = str.blood;
            speed = HelperConstants.speedMultOfWa;
            rotationSpeed = HelperConstants.warriorRotationSpeed;
            isShooter = true;

            positionsDuringSomeTime = new Helpers.Deque<Vector2>(3);//TODO: magic number

            stats = new Statistics();
            //TODO: add limbs

            angles = new List<int>();

            #region Calculate angles

            Debug.Assert(raycastSensors % 2 == 1, "amount of sensors is too even");
            var mul = 0;
            for (int i = 0; i < raycastSensors; i++)
            {
                if (i % 2 == 0)
                {
                    angles.Add(90 - diffBetwSensorsInDeg * mul);
                }
                if (i % 2 == 1)
                {
                    mul += 1;
                    angles.Add(90 + diffBetwSensorsInDeg * mul);
                }
            }

            #endregion Calculate angles
        }

        public void Revive(Vector2 pos)
        {
            gameObject.SetActive(true);
            transform.GetChild(0).gameObject.SetActive(true);
            transform.position = pos;
            bloodRemaining = bloodMax;
            ai.network.ResetState();
        }

        public void Tick(Actions.Action action)
        {
            Tick();
            currentAction = action;
        }

        public void Tick()
        {
            currentAction.Tick();
        }

        public double[] data { get; private set; }
        public double[] prediction { get; private set; }

        public Actions.Action ChooseAction()
        {
            data = GatherDataOfEnvironment();
            prediction = ai.Predict(data);
            Actions.Action action;

            action = PredictionToAction(prediction);
            return action;
        }

        private List<int> angles;

        public readonly int sensorRange = 10;
        public readonly int raycastSensors = 5;
        public readonly int diffBetwSensorsInDeg = 24;

        private double[] GatherDataOfEnvironment()
        {
            var dataData = new List<double>(60);

            #region Raycasts

            var warrEyeData = new double[raycastSensors, 3];
            var obstacleEyeData = new double[raycastSensors, 1];

            var pos = transform.position;

            var warrMask = LayerMask.GetMask(HelperConstants.warriorTag, HelperConstants.projectileTag);
            var obstMask = LayerMask.GetMask(HelperConstants.obstacleTag);

            var warrEyeCount = 0;
            var obstclEyeCount = 0;
            foreach (var angle in angles)
            {
                var dir = transform.TransformDirection(new Vector3(Mathf.Cos(Mathf.Deg2Rad * (angle)), Mathf.Sin(Mathf.Deg2Rad * (angle)), 0).normalized);
                RaycastHit2D hit;

                #region Other warrs

                hit = Physics2D.Raycast(pos, dir, sensorRange, warrMask);
                if (hit)
                {
                    Debug.DrawLine(pos, hit.point);
                    warrEyeData[warrEyeCount, 0] = 1 - (hit.distance / sensorRange); //was: eyeData[j] = Math.Min(1 / hit.distance, 1);
                    if (hit.collider.tag.Equals(HelperConstants.warriorTag))
                    {
                        var other = hit.collider.GetComponent<WarriorController>();

                        warrEyeData[warrEyeCount, 1] = other.team == team ? 0 : 1;//0 if same team else 1
                        warrEyeData[warrEyeCount, 2] = Helpers.NormalizeNumber(Vector2.Angle(other.transform.up, transform.up), 0, 180, false);//4th element is angle between this warrior and other
                                                                                                                                               //eyeData[j + 3] = NormalizeAngle((other.transform.up - transform.up).normalized);
                    }
                    else if (hit.collider.tag.Equals(HelperConstants.projectileTag))
                    {
                        var projectile = hit.collider.GetComponent<ProjectileController>();

                        warrEyeData[warrEyeCount, 1] = -1;//-1 - projectile
                        warrEyeData[warrEyeCount, 2] = Helpers.NormalizeNumber(Vector2.Angle(projectile.transform.up, transform.up), 0, 180, false);
                        //eyeData[j + 3] = NormalizeAngle((transform.TransformPoint(projectile.Direction) - transform.up).normalized);
                    }
                }
                warrEyeCount += 1; //every eye must see distance and what is it

                #endregion Other warrs

                #region Obstacles

                hit = Physics2D.Raycast(pos, dir, sensorRange, obstMask);
                if (hit)
                {
                    Debug.DrawLine(pos, hit.point);
                    warrEyeData[obstclEyeCount, 0] = 1 - (hit.distance / sensorRange); //was: eyeData[j] = Math.Min(1 / hit.distance, 1);
                }
                obstclEyeCount += 1; //every eye must see distance and what is it

                #endregion Obstacles
            }

            #endregion Raycasts

            foreach (var item in warrEyeData)
            {
                dataData.Add(item);
            }
            foreach (var item in obstacleEyeData)
            {
                dataData.Add(item);
            }

            dataData.Add(1 - (1 / bloodRemaining));
            var deltaVector = (playerOwner.transform.position - transform.position).normalized;
            dataData.Add(NormalizeAngle(deltaVector));//magic

            var temp = dataData.ToArray();
            Debug.Assert(HelperConstants.totalAmountOfSensors == temp.Length, $"wrong array length: {HelperConstants.totalAmountOfSensors} != {temp.Length}");
            return temp;
        }

        private float NormalizeAngle(Vector2 deltaVector)
        {
            float angle = transform.eulerAngles.z % 360f;
            if (angle < 0f)
                angle += 360f;

            float rad = Mathf.Atan2(deltaVector.y, deltaVector.x);
            rad *= Mathf.Rad2Deg;

            rad = rad % 360;
            if (rad < 0)
            {
                rad = 360 + rad;
            }

            rad = 90f - rad;
            if (rad < 0f)
            {
                rad += 360f;
            }
            rad = 360 - rad;
            rad -= angle;
            if (rad < 0)
                rad = 360 + rad;
            if (rad >= 180f)
            {
                rad = 360 - rad;
                rad *= -1f;
            }
            rad *= Mathf.Deg2Rad;

            return rad / (Mathf.PI);
        }

        public bool isShooter;
        public Helpers.Deque<Vector2> positionsDuringSomeTime;

        private Actions.Action PredictionToAction(double[] pred)
        {
            Debug.Assert(pred.Length == HelperConstants.totalAmountOfOutputsOfNet);
            /* [0] - angle to rotate
             * [1] - speed
             * [2] - whether to attack
             * [3] - whether to idle
            */

            if (pred[2] >= 1)//attack
            {
                if (!isShooter)
                    return new Actions.AttackMeleeAction(transform.TransformPoint(Vector3.up), 100, 20, this);
                else
                    return new Actions.AttackShootAction(transform.up, 80, 20, this);
            }
            else if (pred[3] >= 1)//idle
            {
                return new Actions.IdleAction();
            }

            var angle = (float)(pred[0] > 0.5 ? 0.5 * rotationSpeed : -0.5 * rotationSpeed);
            var moveBy = (float)pred[1] > 0.5 ? (float)pred[1] : -(float)pred[1];//backwards is slower
            return new Actions.MoveAction(moveBy * speed, angle, this);
        }

        #region Statistics

        public Statistics stats { get; private set; }

        public double GetFitness()
        {
            /*Melee:
             *AddFitnessToThis(collWarr.team == warr.team ? 0 : HelperConstants.fitnessBonusForDyingFromEnemy);//encourage death from enemies
             *AddFitnessToThis(collWarr.team == warr.team ? HelperConstants.fitnessPenaltyForKillingAlly : HelperConstants.fitnessForKillingAnEnemy);//if team the same then
             *
             * Move:
             *  var plOwnerTrPos = moves.PlayerOwner.transform.position;
                    var distMovedTowPl = Vector3.Distance(posBeforeMove, plOwnerTrPos) - Vector3.Distance(moves.transform.position, plOwnerTrPos);

                    var ftns = distMovedTowPl > 0 ? distMovedTowPl * HelperConstants.fitnessMultiplierForApproachingToFlag : 0;
                    if (ftns > 100)
                    {
                        Debug.Log("hui");
                    }
                    var divider = (float)Math.Pow(Vector3.Distance(moves.transform.position, plOwnerTrPos), 2);
                    divider = divider < 1 ? 1 : divider;

                    var toAdd = (ftns / divider);//if approached to owner then encourage

                    moves.AddFitnessToThis(toAdd);
             *
             *
             *
             *
             */
            double fitness = 0;
            try
            {
                fitness += (1 - 1 / ((stats.damageFromEnemy) + 1)) * HelperConstants.fitnessBonusForDyingFromEnemy;

                fitness += (1 - 1 / ((stats.enemyDamage) + 1)) * HelperConstants.fitnessForKillingAnEnemy;

                fitness += (1 - 1 / ((stats.passedToFlagDistCoef) + 1)) * HelperConstants.fitnessMultiplierForApproachingToFlag;

                fitness -= (1 - 1 / ((stats.allyDamage) + 1)) * HelperConstants.fitnessPenaltyForKillingAlly;//more penalty, more substracted from fitness
            }
            catch (DivideByZeroException e)
            {
                Debug.Log(e);
            }

            return fitness;
        }

        #endregion Statistics

        public bool TakeDamage(float toLose)
        {
            transform.GetChild(0).GetComponent<ParticleSystem>().Play();
            bloodRemaining -= toLose;
            if (bloodRemaining <= 0)
            {
                gameObject.SetActive(false);
                return true;
            }
            return false;
        }
    }
}