using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.TerrainAPI;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Assertions;
using Random = UnityEngine.Random;

namespace TerrainGenerator
{
  public class WalkAgent
  {

    private readonly Tile[,] tiles;
    private readonly int width;
    private readonly int height;
    
    private readonly Acre[,] acres;
    private readonly Acre startAcre;
    private readonly int acreSize;

    private readonly CliffTile[] cliffTiles;
    private readonly int maxCliffEat;
    private readonly int minCliffEat;
    private readonly int cliffElevation;
    
    private Vector2Int pos;
    private Vector2Int forward;
    private Vector2Int right;

    private Stack<WalkStep> steps;

    private bool firstStep;
    private bool done;

    private bool notWalkedStartAcre;

    private readonly int maxCliffWalkReverts;
    private int numReverts;
    
    public WalkAgent(Tile[,] tiles, int width, int height,
                     Acre[,] acres, Acre startAcre, int acreSize,
                     CliffTile[] cliffTiles, int maxCliffEat, int minCliffEat,
                     Vector2Int pos, Vector2Int forward, Vector2Int right,
                     int maxCliffWalkReverts)
    {
      this.tiles = tiles;
      this.width = width;
      this.height = height;

      this.acres = acres;
      this.startAcre = startAcre;
      this.acreSize = acreSize;

      this.cliffTiles = cliffTiles;
      this.maxCliffEat = maxCliffEat;
      this.minCliffEat = minCliffEat;
      cliffElevation = startAcre.elevation;

      this.pos = pos;
      this.forward = forward;
      this.right = right;

      steps = new Stack<WalkStep>();

      firstStep = true;
      done = false;

      notWalkedStartAcre = tiles[pos.x, pos.y].acre != startAcre;

      this.maxCliffWalkReverts = maxCliffWalkReverts;
      numReverts = 0;
    }

    public void Step()
    {
      if (done)
      {
        return;
      }

      // First time is special case
      if (firstStep)
      {
        FirstStep();
      }
      else
      {
        var prevStep = steps.Peek();

        if (tiles[pos.x, pos.y].acre == startAcre)
        {
          notWalkedStartAcre = false;
        }
        
        (var validRules, var numRules, var selectedRule) = SelectRule();
        if (prevStep.selectedRule > -1)
        {
          selectedRule = prevStep.selectedRule;
        }
        var tries = prevStep.tries;

        var ruleFound = false;
        CliffTileRule rule = null;
        var trialPos = new Vector2Int(pos.x, pos.y);
        do
        {
          if (tries >= numRules)
          {
            break; // Revert step
          }
          
          var ruleIndex = (selectedRule + tries) % numRules;
          rule = validRules[ruleIndex];
          tries++;

          trialPos = pos + rule.offset;
          if (!IsOutsideMap(trialPos))
          {
            var trialTile = tiles[trialPos.x, trialPos.y];
            ruleFound = IsValidTile(trialTile);
          }

          if (ruleFound)
          {
            // Check if done (cliff collision)
            var t1 = tiles[pos.x + rule.offset.x, pos.y];
            var t2 = tiles[pos.x, pos.y + rule.offset.y];
            var t3 = tiles[pos.x + rule.offset.x, pos.y + rule.offset.y];
            if (t3.isCliff)
            {
              var validMergeTile = false;
              if (forward.x > 0)
              {
                validMergeTile = t3.cliffTile.overlaps[rule.index].fromWest;
              }
              else if (forward.y > 0)
              {
                validMergeTile = t3.cliffTile.overlaps[rule.index].fromNorth;
              }
              else if (forward.y < 0)
              {
                validMergeTile = t3.cliffTile.overlaps[rule.index].fromSouth;
              }
              
              if (validMergeTile)
              {
                t3.isMergeCliff = true;
                t3.mergeCliffs.Add(new Tuple<int, CliffTile>(cliffElevation, cliffTiles[rule.index]));
                var t = tiles[pos.x, pos.y];
                t3.connectedCliffs.Add(t);
                t.connectedCliffs.Add(t3);
                done = true;
                return;
              }
              else
              {
                ruleFound = false;
              }
            }
            else if (t1.isCliff && t2.isCliff)
            {
              t1.isMergeCliff = true;
              t1.mergeCliffs.Add(new Tuple<int, CliffTile>(cliffElevation, t1.cliffTile));
              var t = tiles[pos.x, pos.y];
              t1.connectedCliffs.Add(t);
              t.connectedCliffs.Add(t1);
              done = true;
              return;
            }
          }
        } while (!ruleFound);

        if (ruleFound && IsOutsideMap(trialPos + forward))
        {
           ruleFound = cliffTiles[rule.index].isEndTile;
        }
        
        if (ruleFound)
        {
          prevStep.tries = tries;
          prevStep.selectedRule = selectedRule;
        
          bool setCliffWalked = !tiles[trialPos.x, trialPos.y].acre.cliffWalked &&
                                tiles[trialPos.x, trialPos.y].acre.elevation == cliffElevation;

          // Check if direction needs to be updated
          (var nextForward, var nextRight) = ComputeDirection(pos, trialPos);
          
          steps.Push(
            new WalkStep(this, trialPos, nextForward, nextRight,
              tiles[trialPos.x, trialPos.y],
              cliffTiles[rule.index],
              cliffElevation,
              setCliffWalked,
              tiles[pos.x, pos.y]
            )
          );
          
          // Apply the step
          steps.Peek().apply();
          
          // Check if done (map edge reached)
          if (IsOutsideMap(pos + forward))
          {
            done = true;
            return;
          }
        }
        else
        {
          // No valid rule was found, revert to previous step
          // Unapply the step
          steps.Pop().unapply();
          numReverts++;

          if (numReverts >= maxCliffWalkReverts)
          {
            throw new Exception("No valid cliff walking solution found (MaxCliffWalkReverts).");
          }
          else if (steps.Count == 0)
          {
            throw new Exception("No valid cliff walking solution found (Steps.Count).");
          }
        }
      }
    }

    private void FirstStep()
    {
      // TODO: Choose starting cliff tile more robustly
      if (forward.y > 0)
      {
        CreateWalkStartConditions(24);
      }
      else if (forward.x > 0)
      {
        CreateWalkStartConditions(8);
      }
      else
      {
        throw new InvalidOperationException();
      }
      
      steps.Peek().apply();
      firstStep = false;
    }

    private void CreateWalkStartConditions(int cliffTileIndex)
    {
        Tile mergeTile = null;
        if (notWalkedStartAcre)
        {
          mergeTile = tiles[pos.x, pos.y];
          Assert.IsTrue(mergeTile.isCliff);
          if (!mergeTile.isMergeCliff && mergeTile.elevation == cliffElevation)
          {
            // Remove soon to be internal cliffs from previous cliff walk
            var connected = mergeTile.connectedCliffs;
            if (connected.Count == 2)
            {
              var nextTile = connected[1];
              while (!nextTile.isMergeCliff)
              {
                nextTile.isCliff = false;
                if (nextTile.connectedCliffs.Count == 1)
                {
                  break;
                }
                nextTile = nextTile.connectedCliffs[1];
              }
              // Remove connection from merge tile
              if (nextTile.isMergeCliff)
              {
                var list = nextTile.mergeCliffs;
                int itemToRemove = -1;
                for (int i = 0; i < list.Count; i++)
                {
                  if (list[i].Item1 == mergeTile.elevation)
                  {
                    itemToRemove = i;
                  }
                }
                Assert.IsTrue(itemToRemove >= 0);
                nextTile.mergeCliffs.RemoveAt(itemToRemove);
                if (nextTile.mergeCliffs.Count == 0)
                {
                  nextTile.isMergeCliff = false;
                }
              }
            }
            else
            {
              Assert.IsTrue(connected.Count == 1);
            }
          }
          
          // Set merge tile
          mergeTile.isMergeCliff = true;
          mergeTile.mergeCliffs.Add(new Tuple<int, CliffTile>(cliffElevation, cliffTiles[cliffTileIndex]));
          pos += forward;
        }
        steps.Push(
          new WalkStep(this, new Vector2Int(pos.x, pos.y),
            new Vector2Int(forward.x, forward.y),
            new Vector2Int(right.x, right.y),
            tiles[pos.x, pos.y], cliffTiles[cliffTileIndex], cliffElevation,
            tiles[pos.x, pos.y].acre.elevation == cliffElevation,
            mergeTile
            )
          );
    }
    
    private (List<CliffTileRule>, int, int) SelectRule()
    {
      var rules = tiles[pos.x, pos.y].cliffTile.rules;
      var validRules = new List<CliffTileRule>();
      foreach (var r in rules)
      {
        if (r.direction == forward)
        {
          validRules.Add(r);
        }
      }

      var numRules = validRules.Count;

      return (validRules, numRules, Random.Range(0, numRules));
    }

    private bool IsValidTile(Tile tile)
    {
      bool valid = true;
      
      valid &= tile.acre.elevation >= cliffElevation; // Valid acre check
      valid &= IsOutsideAcre(tile, right * maxCliffEat) && !IsOutsideAcre(tile, right * minCliffEat);
      
      return valid;
    }

    private (Vector2Int forward, Vector2Int right) ComputeDirection(Vector2Int p1, Vector2Int p2)
    {
      if (notWalkedStartAcre)
      {
        return (new Vector2Int(forward.x, forward.y), new Vector2Int(right.x, right.y));
      }
      
      var t1 = tiles[p1.x, p1.y];
      var t2 = tiles[p2.x, p2.y];

      if (t1.acre == t2.acre)
      {
        // Same acre
        return ComputeDirectionInternal(t2);
      }
      else
      {
        // New acre
        return ComputeDirectionExternal(t2);
      }
    }

    private (Vector2Int forward, Vector2Int right) ComputeDirectionInternal(Tile t)
    {
      var acre = t.acre;
      var p = t.pos - acre.pos * acreSize;
      
      if (forward == new Vector2Int(0, 1) && acre.hasSouthCliff)
      {
        if (p.y >= acreSize - 1 - minCliffEat)
        {
          return ComputeDirectionChange(Vector2Int.right);
        }
        else if (p.y >= acreSize - maxCliffEat)
        {
          if (Random.Range(0, maxCliffEat - minCliffEat) == 0)
          {
            return ComputeDirectionChange(Vector2Int.right);
          }
        }
      } 
      else if (forward == Vector2Int.right && acre.hasEastCliff)
      {
        if (p.x >= acreSize - 1 - minCliffEat)
        {
          return ComputeDirectionChange(new Vector2Int(0, -1));
        } 
        else if (p.x >= acreSize - maxCliffEat)
        {
          if (Random.Range(0, maxCliffEat - minCliffEat) == 0)
          {
            return ComputeDirectionChange(new Vector2Int(0, -1));
          }
        }
      }
      return (new Vector2Int(forward.x, forward.y), new Vector2Int(right.x, right.y));
    }

    private (Vector2Int forward, Vector2Int right) ComputeDirectionExternal(Tile t)
    {
      Acre acre = t.acre;
      if (forward == Vector2Int.right)
      {
        if (acre.hasSouthWestCliff || acre.hasWestCliff)
        {
          return (new Vector2Int(0, 1), new Vector2Int(-1, 0));
        }
      }
      else if (forward == new Vector2Int(0, -1))
      {
        if (acre.hasSouthEastCliff || acre.hasSouthCliff)
        {
          return (new Vector2Int(1, 0), new Vector2Int(0, 1));
        }
      }
      return (new Vector2Int(forward.x, forward.y), new Vector2Int(right.x, right.y));
    }

    private (Vector2Int forward, Vector2Int right) ComputeDirectionChange(Vector2Int f)
    {
      return (f, new Vector2Int(-f.y, f.x));
    }
    
    private bool IsOutsideAcre(Tile tile, Vector2Int offset)
    {
      var p = tile.pos + offset;
      return p.x < tile.acre.pos.x * acreSize ||
             p.x >= tile.acre.pos.x * acreSize + acreSize ||
             p.y >= tile.acre.pos.y * acreSize + acreSize ||
             p.y < tile.acre.pos.y * acreSize;
    }

    private bool IsOutsideMap(Vector2Int p)
    {
      return p.x < 0 ||
             p.x >= width ||
             p.y >= height ||
             p.y < 0;
    }
    public bool IsDone()
    {
      return done;
    }

    private class WalkStep
    {
      private readonly WalkAgent agent;
      private readonly Vector2Int pos;
      private readonly Vector2Int forward;
      private readonly Vector2Int right;
      private readonly Vector2Int posBefore;
      private readonly Vector2Int forwardBefore;
      private readonly Vector2Int rightBefore;
      
      private readonly Tile tile;
      private readonly Tile connectedCliff;
      private readonly CliffTile cliffTile;
      private readonly int elevation;

      private readonly bool setCliffWalked;

      public int tries;
      public int selectedRule;
      
      public WalkStep(WalkAgent agent, Vector2Int pos, Vector2Int forward, Vector2Int right, Tile tile, CliffTile cliffTile, int elevation,
                      bool setCliffWalked, Tile connectedCliff)
      {
        this.agent = agent;
        posBefore = agent.pos; 
        forwardBefore = agent.forward;
        rightBefore = agent.right;
        this.pos = pos;
        this.forward = forward;
        this.right = right;
        
        this.tile = tile;
        this.connectedCliff = connectedCliff;
        this.cliffTile = cliffTile;
        this.elevation = elevation;
        this.setCliffWalked = setCliffWalked;
        
        tries = 0;
        selectedRule = -1;
      }

      public void apply()
      {
        agent.pos = pos;
        agent.forward = forward;
        agent.right = right;
        
        tile.cliffTile = cliffTile;
        tile.isCliff = true;
        if (elevation == 0)
        {
          tile.isBeachCliff = true;
        }
        tile.elevation = elevation;
        if (setCliffWalked)
        {
          tile.acre.cliffWalked = true;
        }

        if (connectedCliff != null)
        {
          tile.connectedCliffs.Add(connectedCliff);
          connectedCliff.connectedCliffs.Add(tile);
        }

        tile.modified = true;
      }

      public void unapply()
      {
        agent.pos = posBefore;
        agent.forward = forwardBefore;
        agent.right = rightBefore;
        
        tile.cliffTile = null;
        tile.isCliff = false;
        tile.isBeachCliff = false;
        tile.elevation = tile.acre.elevation;
        if (setCliffWalked)
        {
          tile.acre.cliffWalked = false;
        }

        if (connectedCliff != null)
        {
          tile.connectedCliffs.Remove(connectedCliff);
          connectedCliff.connectedCliffs.Remove(tile);
        }

        tile.modified = true;
      }
    }
  }
}