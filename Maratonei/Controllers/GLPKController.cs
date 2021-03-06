﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using OPTANO.Modeling.Optimization;
using OPTANO.Modeling.Optimization.Solver.GLPK;
using Maratonei.Models;


namespace Maratonei {
    [Route( "api/[controller]" )]
    public class GLPKController : Controller {

        /// <summary>
        /// Requisição para os tipos de status possiveis da solução
        /// </summary>
        [HttpGet( "GetStatus/" )]
        [Route( "GLPK/GetStatus" )]
        public IActionResult GetStatus() {
            Dictionary<string, int> resp = new Dictionary<string, int>( );
            foreach(OPTANO.Modeling.Optimization.Solver.SolutionStatus x in Enum.GetValues( typeof( OPTANO.Modeling.Optimization.Solver.SolutionStatus ) )){
                resp.Add( x.ToString( ), Convert.ToInt32( x ) );
            }
            resp.OrderBy( a => a.Value );
            return Ok( resp );

        }

        /// <summary>
        /// Requisicao para resolucao do glpk
        /// </summary>
        /// <param name="input">Funcao objetiva + lista de restricoes</param>
        /// <returns>Codigo da solução</returns>
        [HttpPost( "Solver/" )]
        [Route( "GLPK/Solver" )]
        public IActionResult Solver( [FromBody] GLPKInput input ) {
            try {
            
                // Cria o modelo e adiciona as variaveis
                Dictionary<string, string> variableKeyName = new Dictionary<string, string>( );
                var model = new Model( );
                List<Variable> variables = new List<Variable>( );
                foreach (var val in input.Variables) {
                    var variable = new Variable( val, 0 );
                    variables.Add( variable );
                    variableKeyName.Add( variable.Name, val );

                }
                
                // Adiciona as restrições ao problema
                Expression expression = Expression.EmptyExpression;
                foreach (var val in input.Restrictions) {
                    for (int i = 0; i < val.Values.Count( ); i++) {
                        expression = val.Values.ElementAt( i ) * variables.ElementAt( i ) + expression;
                    }

                    if (val.Operation == GLPKRestriction.Operator.GreaterOrEqual) {
                        model.AddConstraint( expression >= val.Disponibility );
                    } else {
                        model.AddConstraint( expression <= val.Disponibility );
                    }
                    expression = Expression.EmptyExpression;
                }

                // Adiciona a função objetiva ao modelo
                expression = Expression.EmptyExpression;
                for (int i = 0; i < input.Objective.Values.Count( ); i++) {
                    expression = input.Objective.Values.ElementAt( i ) * variables.ElementAt( i ) + expression;
                }

                if (input.Objective.Operation == GLPKObjective.Operator.Maximize) {
                    model.AddObjective( new Objective( expression, "Z", OPTANO.Modeling.Optimization.Enums.ObjectiveSense.Maximize ) );
                } else {
                    model.AddObjective( new Objective( expression, "Z", OPTANO.Modeling.Optimization.Enums.ObjectiveSense.Minimize ) );
                }

                // Resolve o modelo por meio do GLPK
                var solver = new GLPKSolver( );
                solver.Solve( model );
                var solution = solver.Solve( model );

                // Renomeia as variaveis para as variaveis do problema recebido
                var variablesRenamed = new Dictionary<string, double>( );
                foreach (var val in solution.VariableValues) {
                    variablesRenamed.Add( variableKeyName[ val.Key ], val.Value );
                }
               
                var objectiveRenamed = new Dictionary<string, double>( );
                foreach (var val in solution.ObjectiveValues) {
                    objectiveRenamed.Add( "Z", val.Value );
                }
                
                // Formata a resposta
                var resp = new GLPKOutput {
                    Objectives = objectiveRenamed,
                    Variables = variablesRenamed,
                    Status = solution.Status
                };
                
                // Retorna a resposta
                return Ok( resp );
            } catch {
                // Para qualquer erro na resolução
                return BadRequest( "It was not possible to calculate the simplex" );
            }
        }
    }
}
